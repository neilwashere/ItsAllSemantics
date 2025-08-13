using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

public interface IChatResponder
{
    // Display name used in UI when needed
    string Name { get; }

    // sessionId is used to maintain multi-turn conversation state (e.g., SignalR ConnectionId)
    Task<ChatMessageModel> GetResponseAsync(string userMessage, string sessionId, CancellationToken ct = default);

    // Streaming events for the assistant response. Implementation should persist the final
    // assistant message into the session's conversation state when an End event is emitted.
    IAsyncEnumerable<StreamingChatEvent> StreamResponseAsync(string userMessage, string sessionId, CancellationToken ct = default);
}
