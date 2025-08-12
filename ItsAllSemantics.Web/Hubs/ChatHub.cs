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

        // Stream response via configured responder (echo or SK)
        var sessionId = Context.ConnectionId;
        var ct = Context.ConnectionAborted;
        try
        {
            await Clients.Caller.SendAsync("ReceiveStreamStart", responder.Name);
            await foreach (var piece in responder.StreamResponseAsync(message, sessionId, ct))
            {
                await Clients.Caller.SendAsync("ReceiveStreamDelta", piece);
            }
            await Clients.Caller.SendAsync("ReceiveStreamEnd");
        }
        catch
        {
            // On error, still close the stream and provide a fallback message chunk
            await Clients.Caller.SendAsync("ReceiveStreamDelta", "[Response failed]");
            await Clients.Caller.SendAsync("ReceiveStreamEnd");
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
