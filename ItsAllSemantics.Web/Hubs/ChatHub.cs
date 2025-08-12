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
        var sessionId = Context.ConnectionId;
        try
        {
            var aiMsg = await responder.GetResponseAsync(message, sessionId);
            await Clients.Caller.SendAsync("ReceiveMessage", aiMsg);
        }
        catch
        {
            // Ensure client clears typing indicator even on failure
            var err = new ChatMessageModel("Sorry, I hit an error generating a reply.", "ai", DateTimeOffset.Now);
            await Clients.Caller.SendAsync("ReceiveMessage", err);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (responder is SemanticKernelChatResponder sk)
        {
            sk.RemoveSession(Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
