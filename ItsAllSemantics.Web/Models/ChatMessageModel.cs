namespace ItsAllSemantics.Web.Models;

/// <summary>
/// Represents a single chat message.
/// </summary>
public sealed class ChatMessageModel(string text, string author, DateTimeOffset timestamp)
{
    /// <summary>Message text content.</summary>
    public string Text { get; init; } = text;

    /// <summary>Author identifier (e.g., "user" or "ai").</summary>
    public string Author { get; init; } = author;

    /// <summary>Timestamp when the message was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = timestamp;

    /// <summary>True when the author is the user.</summary>
    public bool IsUser => string.Equals(Author, "user", StringComparison.OrdinalIgnoreCase);

    /// <summary>Optional stable error code if this message represents a failed generation.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Indicates the message content is an error representation (no valid AI response).</summary>
    public bool IsError => ErrorCode is not null;

    /// <summary>Total number of tokens (or token-like delta fragments) produced for this assistant message, if known.</summary>
    public int? TokenCount { get; init; }

    /// <summary>Optional agent identity when Author is an AI system (e.g., specific tool/agent name).</summary>
    public string? Agent { get; init; }
}
