using ItsAllSemantics.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace ItsAllSemantics.Web.Hubs;

public class ChatHub : Hub
{
    public async Task SendMessage(string message)
    {
        // Echo the user's message back for display
        var userMsg = new ChatMessageModel(message, "user", DateTimeOffset.Now);
        await Clients.Caller.SendAsync("ReceiveMessage", userMsg);

        // Naive AI placeholder
        var aiText = $"You said: '{message}'. I'll respond intelligently once SK is wired.";
        var aiMsg = new ChatMessageModel(aiText, "ai", DateTimeOffset.Now);
        await Clients.Caller.SendAsync("ReceiveMessage", aiMsg);
    }
}
