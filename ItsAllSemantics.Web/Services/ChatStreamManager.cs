using System.Collections.Concurrent;
using ItsAllSemantics.Web.Hubs;
using ItsAllSemantics.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ItsAllSemantics.Web.Services;

/// <summary>
/// Manages lifecycle of active streaming chat responses decoupled from hub method invocation so that
/// cancellation (and future concurrent commands) can execute while a stream is in progress.
/// </summary>
public interface IChatStreamManager
{
    Task StartAsync(string connectionId, string userMessage, CancellationToken connectionAborted);
    Task<bool> CancelAsync(string connectionId, string streamId);
    Task DisconnectAsync(string connectionId);
}

internal sealed class ChatStreamManager : IChatStreamManager
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly IChatOrchestrator _orchestrator;
    private readonly ILogger<ChatStreamManager> _log;

    private sealed record Active(string StreamId, CancellationTokenSource Cts, Task Runner);
    private readonly ConcurrentDictionary<string, Active> _active = new();

    public ChatStreamManager(IHubContext<ChatHub> hub, IChatOrchestrator orchestrator, ILogger<ChatStreamManager> log)
    {
        _hub = hub;
        _orchestrator = orchestrator;
        _log = log;
    }

    public Task StartAsync(string connectionId, string userMessage, CancellationToken connectionAborted)
    {
        if (_active.ContainsKey(connectionId))
        {
            _ = _hub.Clients.Client(connectionId)
                .SendAsync("ReceiveStreamError", "Busy", "A response is already in progress.", false);
            return Task.CompletedTask;
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(connectionAborted);

        var runner = Task.Run(async () => await RunStreamAsync(connectionId, userMessage, linkedCts), CancellationToken.None);
        // Placeholder until Start event provides stream id
        _active[connectionId] = new Active(string.Empty, linkedCts, runner);
        return Task.CompletedTask;
    }

    public Task<bool> CancelAsync(string connectionId, string streamId)
    {
        if (_active.TryGetValue(connectionId, out var act))
        {
            if (string.IsNullOrEmpty(act.StreamId) || act.StreamId == streamId)
            {
                _log.LogInformation("[STREAM] Cancel requested connection={ConnectionId} streamId={StreamId}", connectionId, streamId);
                try { act.Cts.Cancel(); } catch { }
                return Task.FromResult(true);
            }
        }
        _log.LogInformation("[STREAM] Cancel ignored connection={ConnectionId} streamId={StreamId}", connectionId, streamId);
        return Task.FromResult(false);
    }

    public Task DisconnectAsync(string connectionId)
    {
        if (_active.TryRemove(connectionId, out var act))
        {
            try { act.Cts.Cancel(); } catch { }
            _log.LogInformation("[STREAM] Disconnected canceled connection={ConnectionId}", connectionId);
        }
        return Task.CompletedTask;
    }

    private async Task RunStreamAsync(string connectionId, string userMessage, CancellationTokenSource linkedCts)
    {
        var token = linkedCts.Token;
        Exception? failure = null;
        string? streamId = null;
        try
        {
            await foreach (var evt in _orchestrator.OrchestrateAsync(userMessage, connectionId, token))
            {
                switch (evt.Kind)
                {
                    case StreamingChatEventKind.Start:
                        streamId = evt.StreamId;
                        _active.AddOrUpdate(connectionId, _ => new Active(streamId, linkedCts, Task.CompletedTask), (_, existing) => existing with { StreamId = streamId });
                        await _hub.Clients.Client(connectionId).SendAsync("ReceiveStreamStart", evt.StreamId, evt.Agent, evt.Meta ?? new Dictionary<string, string>());
                        _log.LogInformation("[STREAM] Start connection={ConnectionId} streamId={StreamId}", connectionId, streamId);
                        break;
                    case StreamingChatEventKind.Delta:
                        if (!string.IsNullOrEmpty(evt.TextDelta))
                        {
                            await _hub.Clients.Client(connectionId).SendAsync("ReceiveStreamDelta", evt.TextDelta, evt.Meta ?? new Dictionary<string, string>());
                        }
                        break;
                    case StreamingChatEventKind.End:
                        await _hub.Clients.Client(connectionId).SendAsync("ReceiveStreamEnd", evt.Meta ?? new Dictionary<string, string>());
                        _log.LogInformation("[STREAM] End connection={ConnectionId} streamId={StreamId}", connectionId, evt.StreamId);
                        _active.TryRemove(connectionId, out _);
                        break;
                    case StreamingChatEventKind.Error:
                        await _hub.Clients.Client(connectionId).SendAsync("ReceiveStreamError", evt.ErrorCode, evt.ErrorMessage, evt.IsTransient ?? false, evt.Meta ?? new Dictionary<string, string>());
                        _log.LogInformation("[STREAM] Error connection={ConnectionId} streamId={StreamId} code={Code}", connectionId, evt.StreamId, evt.ErrorCode);
                        _active.TryRemove(connectionId, out _);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            failure = new OperationCanceledException();
            _log.LogInformation("[STREAM] Canceled (exception) connection={ConnectionId} streamId={StreamId}", connectionId, streamId);
        }
        catch (Exception ex)
        {
            failure = ex;
            _log.LogError(ex, "[STREAM] Unhandled exception connection={ConnectionId} streamId={StreamId}", connectionId, streamId ?? "?");
        }
        finally
        {
            if (failure is OperationCanceledException)
            {
                await _hub.Clients.Client(connectionId).SendAsync("ReceiveStreamError", "Canceled", "Generation canceled.", false, new Dictionary<string, string> { { "status", "canceled" } });
            }
            else if (failure is not null)
            {
                await _hub.Clients.Client(connectionId).SendAsync("ReceiveStreamError", "Unhandled", "Something went wrong while generating a response.", false, new Dictionary<string, string> { { "status", "unhandled" } });
            }
            _active.TryRemove(connectionId, out _);
        }
    }
}
