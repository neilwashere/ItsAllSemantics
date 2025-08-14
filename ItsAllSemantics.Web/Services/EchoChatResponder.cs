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
        // If the user prefixes the message with /long we generate a long pseudo-token stream
        if (userMessage.StartsWith("/long", StringComparison.OrdinalIgnoreCase))
        {
            var core = userMessage.Length > 5 ? userMessage[5..].Trim() : "test";
            var para = $"You requested a long streaming response for: {core}. This is synthetic filler text designed to help exercise the cancellation pathway and UI responsiveness. ";
            // Build a large body ~50 paragraphs
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 50; i++)
            {
                sb.Append("Paragraph ").Append(i + 1).Append(':').Append(' ').Append(para);
                sb.Append("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed non risus. Suspendisse lectus tortor, dignissim sit amet, adipiscing nec, ultricies sed, dolor. ");
                sb.Append("Cras elementum ultrices diam. Maecenas ligula massa, varius a, semper congue, euismod non, mi. Proin porttitor, orci nec nonummy molestie, enim est eleifend mi, non fermentum diam nisl sit amet erat. ");
            }
            var big = sb.ToString();
            // Stream in small chunks to maximize opportunities to cancel
            const int slice = 40;
            for (int ofs = 0; ofs < big.Length; ofs += slice)
            {
                ct.ThrowIfCancellationRequested();
                var len = Math.Min(slice, big.Length - ofs);
                var piece = big.AsSpan(ofs, len).ToString();
                await Task.Delay(30, ct); // simulate token latency
                yield return new StreamingChatEvent { Kind = StreamingChatEventKind.Delta, StreamId = streamId, Agent = Name, TextDelta = piece };
            }
            yield return new StreamingChatEvent { Kind = StreamingChatEventKind.End, StreamId = streamId, Agent = Name };
            yield break;
        }

        // Default short echo path
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
