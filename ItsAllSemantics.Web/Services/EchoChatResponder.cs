using ItsAllSemantics.Web.Models;

namespace ItsAllSemantics.Web.Services;

public sealed class EchoChatResponder : IChatResponder
{
    public string Name => "ai";

    public Task<ChatMessageModel> GetResponseAsync(string userMessage, string sessionId, CancellationToken ct = default)
    {
        var reply = new ChatMessageModel($"You said: '{userMessage}'. Echo responder.", "ai", DateTimeOffset.Now);
        return Task.FromResult(reply);
    }

    public async IAsyncEnumerable<StreamingChatEvent> StreamResponseAsync(string userMessage, string sessionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamId = Guid.NewGuid().ToString("N");

        yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Start, StreamId = streamId, Agent = Name };

        var chunks = new[] { "You said: ", "'", userMessage, "'", ". ", "Echo responder." };
        foreach (var c in chunks)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(80, ct);
            yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Delta, StreamId = streamId, Agent = Name, TextDelta = c };
        }

        yield return new StreamingChatEvent { Kind = StreamingChatEventKind.End, StreamId = streamId, Agent = Name };
    }
}
