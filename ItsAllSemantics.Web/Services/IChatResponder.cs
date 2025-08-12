using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

public interface IChatResponder
{
    // sessionId is used to maintain multi-turn conversation state (e.g., SignalR ConnectionId)
    Task<ChatMessageModel> GetResponseAsync(string userMessage, string sessionId, CancellationToken ct = default);
}
