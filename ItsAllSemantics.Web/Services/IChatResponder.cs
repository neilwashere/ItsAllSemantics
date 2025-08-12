using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

public interface IChatResponder
{
    // Display name used in UI when needed
    string Name { get; }

    // sessionId is used to maintain multi-turn conversation state (e.g., SignalR ConnectionId)
    Task<ChatMessageModel> GetResponseAsync(string userMessage, string sessionId, CancellationToken ct = default);

    // Streaming tokens/fragments of the assistant response. Implementation should also persist
    // the final assistant message into the session's conversation state when enumeration completes.
    IAsyncEnumerable<string> StreamResponseAsync(string userMessage, string sessionId, CancellationToken ct = default);
}
