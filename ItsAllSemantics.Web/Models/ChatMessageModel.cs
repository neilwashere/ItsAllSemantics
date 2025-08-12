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
}
