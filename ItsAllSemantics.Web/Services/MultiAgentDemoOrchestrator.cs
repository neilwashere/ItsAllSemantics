using System.Collections.Concurrent;
using System.Threading.Channels;
using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

/// <summary>
/// Demonstration orchestrator that runs multiple lightweight "agents" concurrently and multiplexes
/// their outputs onto a single stream (single streamId). Each agent is synthetic (echo / http fetch)
/// to exercise concurrency and metadata without relying yet on SK agent orchestration primitives.
/// </summary>
internal sealed class MultiAgentDemoOrchestrator : IChatOrchestrator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyList<IDemoAgent> _agents;
    private readonly string _streamAgentLabel;

    public MultiAgentDemoOrchestrator(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _agents = new List<IDemoAgent>
        {
            new EchoWordsAgent("Echoer"),
            new HttpSnippetAgent("Fetcher", httpClientFactory, new Uri("https://example.com/"))
        };
        _streamAgentLabel = "orchestrator";
    }

    public async IAsyncEnumerable<StreamingChatEvent> OrchestrateAsync(string userMessage, string sessionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamId = Guid.NewGuid().ToString("N");
        int globalSeq = 0;
        var perAgentSeq = new ConcurrentDictionary<string, int>();
        var perAgentTokenCounts = new ConcurrentDictionary<string, int>();
        var channel = Channel.CreateUnbounded<StreamingChatEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linkedCts.Token;

        var tasks = _agents.Select(agent => Task.Run(async () =>
        {
            try
            {
                await foreach (var piece in agent.GenerateAsync(userMessage, token))
                {
                    token.ThrowIfCancellationRequested();
                    var seq = perAgentSeq.AddOrUpdate(agent.Name, 0, (_, old) => old + 1);
                    perAgentTokenCounts.AddOrUpdate(agent.Name, 1, (_, old) => old + 1);
                    var meta = new Dictionary<string, string>
                    {
                        ["agent"] = agent.Name,
                        ["agentSeq"] = seq.ToString(),
                        ["mode"] = "concurrent",
                        ["status"] = "agent-delta"
                    };
                    await channel.Writer.WriteAsync(new StreamingChatEvent
                    {
                        Kind = StreamingChatEventKind.Delta,
                        StreamId = streamId,
                        Agent = agent.Name,
                        TextDelta = piece,
                        Meta = meta
                    }, token);
                }
                var endMeta = new Dictionary<string, string>
                {
                    ["agent"] = agent.Name,
                    ["status"] = "agent-end",
                    ["agentSeq"] = perAgentSeq.TryGetValue(agent.Name, out var last) ? last.ToString() : "0",
                    ["mode"] = "concurrent"
                };
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Kind = StreamingChatEventKind.Delta,
                    StreamId = streamId,
                    Agent = agent.Name,
                    TextDelta = string.Empty,
                    Meta = endMeta
                }, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var errMeta = new Dictionary<string, string>
                {
                    ["agent"] = agent.Name,
                    ["status"] = "agent-error",
                    ["mode"] = "concurrent"
                };
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Kind = StreamingChatEventKind.Error,
                    StreamId = streamId,
                    Agent = agent.Name,
                    ErrorCode = "AgentError",
                    ErrorMessage = ex.Message,
                    Meta = errMeta
                }, CancellationToken.None);
            }
        }, token)).ToArray();

        yield return new StreamingChatEvent
        {
            Kind = StreamingChatEventKind.Start,
            StreamId = streamId,
            Agent = _streamAgentLabel,
            Meta = new Dictionary<string, string>
            {
                ["status"] = "start",
                ["mode"] = "concurrent",
                ["agent"] = _streamAgentLabel,
                ["globalSeq"] = globalSeq.ToString()
            }
        };
        globalSeq++;

        _ = Task.WhenAll(tasks).ContinueWith(_ => channel.Writer.TryComplete());

        await foreach (var evt in channel.Reader.ReadAllAsync(token))
        {
            var meta = evt.Meta is null ? new Dictionary<string, string>() : new Dictionary<string, string>(evt.Meta);
            meta["globalSeq"] = globalSeq.ToString();
            // Re-emit as a new event object (avoid assigning init-only Meta)
            yield return new StreamingChatEvent
            {
                Kind = evt.Kind,
                StreamId = evt.StreamId,
                Agent = evt.Agent,
                Role = evt.Role,
                TextDelta = evt.TextDelta,
                FinalText = evt.FinalText,
                Timestamp = evt.Timestamp,
                ErrorCode = evt.ErrorCode,
                ErrorMessage = evt.ErrorMessage,
                IsTransient = evt.IsTransient,
                Meta = meta
            };
            globalSeq++;
        }

        var summaryBuilder = new System.Text.StringBuilder();
        foreach (var agent in _agents)
        {
            if (perAgentTokenCounts.TryGetValue(agent.Name, out var count))
            {
                summaryBuilder.Append(agent.Name).Append(':').Append(count).Append(' ');
            }
        }
        var aggregateMeta = new Dictionary<string, string>
        {
            ["status"] = "orchestration-end",
            ["mode"] = "concurrent",
            ["agent"] = _streamAgentLabel,
            ["globalSeq"] = globalSeq.ToString(),
            ["tokenCount"] = (globalSeq - 1).ToString()
        };

        yield return new StreamingChatEvent
        {
            Kind = StreamingChatEventKind.End,
            StreamId = streamId,
            Agent = _streamAgentLabel,
            FinalText = summaryBuilder.ToString().TrimEnd(),
            Meta = aggregateMeta
        };
    }

    private interface IDemoAgent
    {
        string Name { get; }
        IAsyncEnumerable<string> GenerateAsync(string userMessage, CancellationToken ct);
    }

    private sealed class EchoWordsAgent : IDemoAgent
    {
        public string Name { get; }
        public EchoWordsAgent(string name) => Name = name;
        public async IAsyncEnumerable<string> GenerateAsync(string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var words = (userMessage ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) words = new[] { "(empty)" };
            foreach (var w in words)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(40, ct);
                yield return w + ' ';
            }
        }
    }

    private sealed class HttpSnippetAgent : IDemoAgent
    {
        public string Name { get; }
        private readonly IHttpClientFactory _factory;
        private readonly Uri _uri;
        public HttpSnippetAgent(string name, IHttpClientFactory factory, Uri uri)
        {
            Name = name; _factory = factory; _uri = uri;
        }
        public async IAsyncEnumerable<string> GenerateAsync(string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            string content;
            try
            {
                using var client = _factory.CreateClient("MultiAgentFetcher");
                client.Timeout = TimeSpan.FromSeconds(3);
                content = await client.GetStringAsync(_uri, ct);
            }
            catch
            {
                content = "Fetched placeholder content for demo concurrency.";
            }
            content = content.Replace('\n', ' ');
            var max = Math.Min(content.Length, 180);
            const int chunk = 20;
            for (int i = 0; i < max; i += chunk)
            {
                ct.ThrowIfCancellationRequested();
                var len = Math.Min(chunk, max - i);
                var piece = content.Substring(i, len);
                await Task.Delay(55, ct);
                yield return piece;
            }
        }
    }
}
