using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

/// <summary>
/// Initial orchestrator: wraps a single <see cref="IChatResponder"/>, enriching events with
/// multiplex-friendly metadata (agent/global sequencing, status labels) while preserving existing shape.
/// </summary>
internal sealed class SingleAgentChatOrchestrator : IChatOrchestrator
{
    private readonly IChatResponder _responder;

    public SingleAgentChatOrchestrator(IChatResponder responder)
    {
        _responder = responder;
    }

    public async IAsyncEnumerable<StreamingChatEvent> OrchestrateAsync(string userMessage, string sessionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        int globalSeq = 0;
        int agentSeq = 0;
        var agentName = _responder.Name;

        await foreach (var evt in _responder.StreamResponseAsync(userMessage, sessionId, ct))
        {
            var meta = evt.Meta is null ? new Dictionary<string, string>() : new Dictionary<string, string>(evt.Meta);
            meta["agent"] = agentName; // ensure presence
            meta["globalSeq"] = globalSeq.ToString();
            meta["status"] = evt.Kind switch
            {
                StreamingChatEventKind.Start => "start",
                StreamingChatEventKind.Delta => "delta",
                StreamingChatEventKind.End => "orchestration-end",
                StreamingChatEventKind.Error => "error",
                _ => "unknown"
            };
            if (evt.Kind == StreamingChatEventKind.Delta)
            {
                meta["agentSeq"] = agentSeq.ToString();
            }
            else if (evt.Kind == StreamingChatEventKind.End)
            {
                // agentSeq has already counted all emitted deltas
                meta["tokenCount"] = agentSeq.ToString();
            }

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
            if (evt.Kind == StreamingChatEventKind.Delta) agentSeq++;
        }
    }
}
