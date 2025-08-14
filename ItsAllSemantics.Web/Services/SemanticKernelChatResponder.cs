using ItsAllSemantics.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Text;
using System.Collections.Concurrent;

namespace ItsAllSemantics.Web.Services;

public sealed class SemanticKernelChatResponder : IChatResponder
{
    private readonly SemanticKernelOptions _options;
    private readonly Kernel _kernel;
    private readonly ChatCompletionAgent _agent;
    private readonly ConcurrentDictionary<string, AgentThread> _threads = new();
    private readonly IChatCompletionService _chat;
    private readonly ConcurrentDictionary<string, ChatHistory> _histories = new();
    private readonly IChatExceptionTranslator _exceptionTranslator;

    public SemanticKernelChatResponder(IOptions<SemanticKernelOptions> options, IChatExceptionTranslator? exceptionTranslator = null)
    {
        _options = options.Value;
        var builder = Kernel.CreateBuilder();
        if (string.Equals(_options.Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: _options.Model,
                endpoint: _options.Endpoint,
                apiKey: _options.ApiKey);
        }
        else
        {
            builder.AddOpenAIChatCompletion(
                modelId: _options.Model,
                apiKey: _options.ApiKey);
        }
        _kernel = builder.Build();
        _chat = _kernel.GetRequiredService<IChatCompletionService>();

        _agent = new ChatCompletionAgent
        {
            Name = "AImee",
            Instructions = "You are a concise, helpful assistant. Keep answers short unless asked to elaborate.",
            Kernel = _kernel
        };
        _exceptionTranslator = exceptionTranslator ?? new DefaultChatExceptionTranslator();
    }

    private AgentThread GetOrCreateThread(string sessionId)
    {
        return _threads.GetOrAdd(sessionId, static _ =>
            new ChatHistoryAgentThread([new ChatMessageContent(AuthorRole.System, "You are a helpful assistant. Be concise.")]));
    }

    private ChatHistory GetOrCreateHistory(string sessionId)
    {
        return _histories.GetOrAdd(sessionId, static _ =>
        {
            var h = new ChatHistory();
            h.Add(new ChatMessageContent(AuthorRole.System, "You are a helpful assistant. Be concise."));
            return h;
        });
    }

    public async Task<ChatMessageModel> GetResponseAsync(string userMessage, string sessionId, CancellationToken ct = default)
    {
        var thread = GetOrCreateThread(sessionId);

        var user = new ChatMessageContent(AuthorRole.User, userMessage);

        ChatMessageContent? last = null;
        await foreach (var response in _agent.InvokeAsync(user, thread, options: null, cancellationToken: ct))
        {
            last = response;
        }

        var content = last?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content)) content = "(No response)";
        return new ChatMessageModel(content, Name, DateTimeOffset.Now);
    }

    public string Name => _agent.Name ?? "ai";

    public async IAsyncEnumerable<StreamingChatEvent> StreamResponseAsync(
        string userMessage,
        string sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var history = GetOrCreateHistory(sessionId);
        history.Add(new ChatMessageContent(AuthorRole.User, userMessage));

        var streamId = Guid.NewGuid().ToString("N");
        yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Start, StreamId = streamId, Agent = Name };

        var builder = new System.Text.StringBuilder();
        Exception? failure = null;
        var stream = _chat.GetStreamingChatMessageContentsAsync(history, executionSettings: null, _kernel, ct);
        var e = stream.GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool moved;
                try
                {
                    moved = await e.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }
                if (!moved) break;

                if (e.Current is StreamingChatMessageContent msg)
                {
                    bool any = false;
                    if (msg.Items is not null)
                    {
                        foreach (var item in msg.Items)
                        {
                            if (item is StreamingTextContent st && !string.IsNullOrEmpty(st.Text))
                            {
                                builder.Append(st.Text);
                                yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Delta, StreamId = streamId, Agent = Name, TextDelta = st.Text };
                                any = true;
                            }
                        }
                    }
                    if (!any && !string.IsNullOrEmpty(msg.Content))
                    {
                        builder.Append(msg.Content);
                        yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Delta, StreamId = streamId, Agent = Name, TextDelta = msg.Content };
                    }
                }
            }
        }
        finally
        {
            await e.DisposeAsync();
        }

        if (failure is null)
        {
            var final = builder.ToString();
            history.Add(new ChatMessageContent(AuthorRole.Assistant, final));
            yield return new StreamingChatEvent { Kind = StreamingChatEventKind.End, StreamId = streamId, Agent = Name, FinalText = final };
        }
        else
        {
            var info = _exceptionTranslator.Translate(failure);
            if (failure is not OperationCanceledException)
            {
                history.Add(new ChatMessageContent(AuthorRole.Assistant, $"[error:{info.Code}] {info.Message}"));
            }
            yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Error, StreamId = streamId, Agent = Name, ErrorCode = info.Code, ErrorMessage = info.Message, IsTransient = info.IsTransient };
        }
    }

    public void RemoveSession(string sessionId)
    {
        _threads.TryRemove(sessionId, out _);
    }
}

public sealed class SemanticKernelOptions
{
    public string Provider { get; set; } = "OpenAI"; // or AzureOpenAI
    public string Model { get; set; } = "gpt-4o-mini";
    public string Endpoint { get; set; } = string.Empty; // for AzureOpenAI
    public string ApiKey { get; set; } = string.Empty;
}
