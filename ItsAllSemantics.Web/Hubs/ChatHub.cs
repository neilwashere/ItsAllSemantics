using ItsAllSemantics.Web.Models;
using ItsAllSemantics.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ItsAllSemantics.Web.Hubs;

/// <summary>
/// SignalR Hub endpoint for chat interactions. Streaming execution is delegated to IChatStreamManager
/// so that cancellation can occur while a stream is active.
/// </summary>
public class ChatHub(IChatResponder responder, IChatStreamManager streamManager, ILogger<ChatHub> logger) : Hub
{
    public async Task SendMessage(string message)
    {
        logger.LogInformation("[HUB] SendMessage connection={ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("ReceiveMessage", new ChatMessageModel(message, "user", DateTimeOffset.Now));
        await streamManager.StartAsync(Context.ConnectionId, message, Context.ConnectionAborted);
    }

    public async Task CancelStream(string streamId)
    {
        var ok = await streamManager.CancelAsync(Context.ConnectionId, streamId);
        if (!ok)
        {
            logger.LogInformation("[HUB] Cancel ignored connection={ConnectionId} streamId={StreamId}", Context.ConnectionId, streamId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (responder is SemanticKernelChatResponder sk)
        {
            sk.RemoveSession(Context.ConnectionId);
        }
        await streamManager.DisconnectAsync(Context.ConnectionId);
        logger.LogInformation("[HUB] Disconnected connection={ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
