using ItsAllSemantics.Web.Models;
using ItsAllSemantics.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace ItsAllSemantics.Web.Hubs;

public class ChatHub(IChatResponder responder) : Hub
{
    public async Task SendMessage(string message)
    {
        // Echo the user's message back for display
        var userMsg = new ChatMessageModel(message, "user", DateTimeOffset.Now);
        await Clients.Caller.SendAsync("ReceiveMessage", userMsg);

        // Get response via configured responder (echo or SK)
        var aiMsg = await responder.GetResponseAsync(message);
        await Clients.Caller.SendAsync("ReceiveMessage", aiMsg);
    }
}
