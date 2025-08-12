using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

public interface IChatResponder
{
    Task<ChatMessageModel> GetResponseAsync(string userMessage, CancellationToken ct = default);
}
