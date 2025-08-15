using System.Collections.Concurrent;
using System.Threading.Channels;
using ItsAllSemantics.Web.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.Concurrent;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ItsAllSemantics.Web.Services;

/// <summary>
/// Clean reimplementation of concurrent SK orchestrator: minimal, structured, compile-safe.
/// Features: per-agent kernels, streaming + end markers, preflight diagnostic, synthetic fallback.
/// </summary>
internal sealed class SemanticKernelConcurrentOrchestrator : IChatOrchestrator
{
    private readonly SemanticKernelOptions _options;
    private readonly ChatCompletionAgent[] _agents;
    private readonly ILogger<SemanticKernelConcurrentOrchestrator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ChatHistory> _histories = new();

    public SemanticKernelConcurrentOrchestrator(IOptions<SemanticKernelOptions> options, ILogger<SemanticKernelConcurrentOrchestrator> logger, ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("SemanticKernel ApiKey is missing - orchestration cannot function without a connector API key. Check configuration 'SemanticKernel:ApiKey'.");
            throw new InvalidOperationException("SemanticKernel ApiKey is missing. Set SemanticKernel:ApiKey in configuration or environment variables.");
        }

        var agentConfigs = new (string Name, string Instructions)[]
        {
            ("Summarizer", "Summarize the user's request very succinctly."),
            ("Expander", "Expand on the user's request with helpful extra context in two concise sentences."),
        };

        var list = new List<ChatCompletionAgent>();
        foreach (var (name, instructions) in agentConfigs)
        {
            var builder = Kernel.CreateBuilder();
            // register logger factory into kernel services so connectors can emit logs
            try { builder.Services.AddSingleton<ILoggerFactory>(_loggerFactory); } catch { /* best-effort */ }

            if (string.Equals(_options.Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
                builder.AddAzureOpenAIChatCompletion(_options.Model, _options.Endpoint, _options.ApiKey);
            else
                builder.AddOpenAIChatCompletion(_options.Model, _options.ApiKey);

            var k = builder.Build();
            list.Add(new ChatCompletionAgent
            {
                Name = name,
                Instructions = instructions,
                Description = $"Agent that can {name}",
                Kernel = k,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings { MaxTokens = 2000 })
            });
            _logger.LogInformation("Agent {Agent} created model={Model} provider={Provider} keyPresent={HasKey}", name, _options.Model, _options.Provider, !string.IsNullOrEmpty(_options.ApiKey));
        }
        _agents = list.ToArray();
    }

    private ChatHistory History(string sessionId) => _histories.GetOrAdd(sessionId, _ =>
    {
        var h = new ChatHistory();
        h.Add(new ChatMessageContent(AuthorRole.System, "You are cooperating with other specialized assistants."));
        return h;
    });

    public async IAsyncEnumerable<StreamingChatEvent> OrchestrateAsync(string userMessage, string sessionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var history = History(sessionId);
        history.Add(new ChatMessageContent(AuthorRole.User, userMessage));
        var streamId = Guid.NewGuid().ToString("N");
        int globalSeq = 0;
        var perAgentSeq = new ConcurrentDictionary<string, int>();
        var agentProduced = new ConcurrentDictionary<string, bool>();
        var channel = Channel.CreateUnbounded<StreamingChatEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        yield return new StreamingChatEvent
        {
            Kind = StreamingChatEventKind.Start,
            StreamId = streamId,
            Agent = "orchestrator",
            Meta = new Dictionary<string, string> { { "status", "start" }, { "mode", "concurrent" }, { "agent", "orchestrator" }, { "globalSeq", globalSeq.ToString() } }
        };
        globalSeq++;

        // CORE STREAMING REFACTOR: from this point we launch orchestration concurrently
        // and immediately consume the channel so deltas flow to the caller as they arrive.
        await using var runtime = new InProcessRuntime();
        await runtime.StartAsync(ct);

        var orchestration = new ConcurrentOrchestration(_agents)
        {
            Name = "ConcurrentDemo",
            LoggerFactory = _loggerFactory,
            StreamingResponseCallback = async (msg, isFinal) =>
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var agent = msg.AuthorName ?? "unknown";
                    var content = msg.Content ?? string.Empty;
                    if (!string.IsNullOrEmpty(content))
                    {
                        _logger.LogDebug("Streaming delta for {Agent} len={Len} isFinal={IsFinal} content={Content}", agent, content.Length, isFinal, content);
                        agentProduced[agent] = true;
                        var seq = perAgentSeq.AddOrUpdate(agent, 0, (_, o) => o + 1);
                        await channel.Writer.WriteAsync(new StreamingChatEvent
                        {
                            Kind = StreamingChatEventKind.Delta,
                            StreamId = streamId,
                            Agent = agent,
                            TextDelta = content,
                            Meta = new Dictionary<string, string> { { "agent", agent }, { "agentSeq", seq.ToString() }, { "mode", "concurrent" }, { "status", "agent-delta" } }
                        }, ct);
                        // output observed
                    }
                    if (isFinal)
                    {
                        _logger.LogDebug("Streaming final chunk signalled for {Agent}", agent);
                        var last = perAgentSeq.TryGetValue(agent, out var l) ? l : 0;
                        await channel.Writer.WriteAsync(new StreamingChatEvent
                        {
                            Kind = StreamingChatEventKind.Delta,
                            StreamId = streamId,
                            Agent = agent,
                            TextDelta = string.Empty,
                            Meta = new Dictionary<string, string> { { "agent", agent }, { "agentSeq", last.ToString() }, { "mode", "concurrent" }, { "status", "agent-end" } }
                        }, ct);
                        // final end marker
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Streaming callback error"); }
            },
            ResponseCallback = async resp =>
            {
                if (ct.IsCancellationRequested) return;
                var agent = resp.AuthorName ?? resp.Role.ToString();
                try { await Task.Delay(150, ct); } catch { }
                if (agentProduced.ContainsKey(agent)) return; // streaming already produced output
                var full = resp.Content ?? string.Empty;
                _logger.LogDebug("Non-streaming response for {Agent} len={Len} content={Content}", agent, full.Length, full);
                if (!string.IsNullOrWhiteSpace(full))
                {
                    var seq = perAgentSeq.AddOrUpdate(agent, 0, (_, o) => o + 1);
                    await channel.Writer.WriteAsync(new StreamingChatEvent
                    {
                        Kind = StreamingChatEventKind.Delta,
                        StreamId = streamId,
                        Agent = agent,
                        TextDelta = full,
                        Meta = new Dictionary<string, string> { { "agent", agent }, { "agentSeq", seq.ToString() }, { "mode", "concurrent" }, { "status", "agent-delta" } }
                    }, ct);

                    // non-streaming full output observed
                    var last = perAgentSeq.TryGetValue(agent, out var l) ? l : 0;
                    await channel.Writer.WriteAsync(new StreamingChatEvent
                    {
                        Kind = StreamingChatEventKind.Delta,
                        StreamId = streamId,
                        Agent = agent,
                        TextDelta = string.Empty,
                        Meta = new Dictionary<string, string> { { "agent", agent }, { "agentSeq", last.ToString() }, { "mode", "concurrent" }, { "status", "agent-end" } }
                    }, ct);
                }
            }
        };

        var orchestrationTask = Task.Run(async () =>
        {
            try
            {
                await orchestration.InvokeAsync(userMessage ?? string.Empty, runtime, ct);
            }
            catch (OperationCanceledException)
            {
                // cancellation handled by consumer
            }
            catch (Exception ex)
            {
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Kind = StreamingChatEventKind.Error,
                    StreamId = streamId,
                    Agent = "orchestrator",
                    ErrorCode = "InvokeError",
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                // Wait for runtime to settle but don't block streaming earlier
                try { await runtime.RunUntilIdleAsync(); } catch (Exception ex) { _logger.LogError(ex, "Runtime idle error"); }
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None); // separate from ct so we can finish cleanup

        // Immediately stream out any events as they are written.
        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
        {
            var meta = evt.Meta == null ? new Dictionary<string, string>() : new Dictionary<string, string>(evt.Meta);
            meta["globalSeq"] = globalSeq.ToString();
            yield return new StreamingChatEvent { Kind = evt.Kind, StreamId = evt.StreamId, Agent = evt.Agent, TextDelta = evt.TextDelta, ErrorCode = evt.ErrorCode, ErrorMessage = evt.ErrorMessage, Meta = meta };
            globalSeq++;
        }

        // Ensure orchestration task has finished (observe exceptions if any after streaming drained)
        try { await orchestrationTask; } catch (Exception ex) { _logger.LogError(ex, "Orchestration task error after stream drain"); }

        yield return new StreamingChatEvent
        {
            Kind = StreamingChatEventKind.End,
            StreamId = streamId,
            Agent = "orchestrator",
            Meta = new Dictionary<string, string> { { "status", "orchestration-end" }, { "mode", "concurrent" }, { "agent", "orchestrator" }, { "globalSeq", globalSeq.ToString() } }
        };
    }

}
