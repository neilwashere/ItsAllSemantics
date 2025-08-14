using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

/// <summary>
/// Orchestrates one (and later multiple) agents while emitting a single logical streaming sequence.
/// Multiplexing is expressed via metadata on <see cref="StreamingChatEvent"/>.
/// </summary>
public interface IChatOrchestrator
{
    /// <summary>
    /// Execute orchestration for a user message within a session. Returns a single logical stream
    /// of events (single streamId) whose deltas can belong to one or more agents.
    /// </summary>
    IAsyncEnumerable<StreamingChatEvent> OrchestrateAsync(string userMessage, string sessionId, CancellationToken ct = default);
}
