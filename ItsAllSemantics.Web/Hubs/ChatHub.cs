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
            await foreach (var evt in responder.StreamResponseAsync(message, sessionId, ct))
            {
                switch (evt.Kind)
                {
                    case StreamingChatEventKind.Start:
                        await Clients.Caller.SendAsync("ReceiveStreamStart", evt.Agent);
                        break;
                    case StreamingChatEventKind.Delta:
                        if (!string.IsNullOrEmpty(evt.TextDelta))
                        {
                            await Clients.Caller.SendAsync("ReceiveStreamDelta", evt.TextDelta);
                            // Force immediate delivery by yielding control
                            await Task.Yield();
                        }
                        break;
                    case StreamingChatEventKind.End:
                        await Clients.Caller.SendAsync("ReceiveStreamEnd");
                        break;
                    case StreamingChatEventKind.Error:
                        await Clients.Caller.SendAsync("ReceiveStreamError", evt.ErrorCode, evt.ErrorMessage, evt.IsTransient ?? false);
                        break;
                }
            }
        }
        catch
        {
            // On unexpected hub-level error emit generic error event
            await Clients.Caller.SendAsync("ReceiveStreamError", "Unhandled", "Something went wrong while generating a response.", false);
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
