namespace ItsAllSemantics.Web.Models;

public enum StreamingChatEventKind
{
    Start,
    Delta,
    End,
    Error
}

public sealed class StreamingChatEvent
{
    public required StreamingChatEventKind Kind { get; init; }
    public required string StreamId { get; init; }
    public string Agent { get; init; } = "ai";
    public string Role { get; init; } = "assistant";
    public string? TextDelta { get; init; }
    public string? FinalText { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public IDictionary<string, string>? Meta { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool? IsTransient { get; init; }
}
