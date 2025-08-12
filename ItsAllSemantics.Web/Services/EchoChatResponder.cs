using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

public sealed class EchoChatResponder : IChatResponder
{
    public Task<ChatMessageModel> GetResponseAsync(string userMessage, CancellationToken ct = default)
    {
        var reply = new ChatMessageModel($"You said: '{userMessage}'. Echo responder.", "ai", DateTimeOffset.Now);
        return Task.FromResult(reply);
    }
}
