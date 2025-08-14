using System.Net;

namespace ItsAllSemantics.Web.Services;

/// <summary>
/// Translates exceptions thrown during chat streaming into structured error metadata.
/// </summary>
public interface IChatExceptionTranslator
{
    ChatErrorInfo Translate(Exception exception);
}

/// <summary>
/// Structured error info used to populate an Error kind StreamingChatEvent.
/// </summary>
/// <param name="Code">Stable code (PascalCase) suitable for telemetry & client branching.</param>
/// <param name="Message">User-friendly, sanitized message.</param>
/// <param name="IsTransient">Indicates caller may retry immediately.</param>
public readonly record struct ChatErrorInfo(string Code, string Message, bool IsTransient);

/// <summary>
/// Default implementation performing coarse-grained classification of common failures.
/// </summary>
public sealed class DefaultChatExceptionTranslator : IChatExceptionTranslator
{
    public ChatErrorInfo Translate(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => new("Canceled", "Generation canceled.", false),
            HttpRequestException httpEx => ClassifyHttp(httpEx),
            // Fallback
            _ => new("Unhandled", "Something went wrong while generating a response.", false)
        };
    }

    private static ChatErrorInfo ClassifyHttp(HttpRequestException ex)
    {
        var status = ex.StatusCode;
        if (status is null)
        {
            return new("UpstreamHttp", "Network error talking to AI service.", true);
        }
        bool transient = (int)status >= 500 || status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests;
        string msg = transient ? "Temporary upstream issue. Please try again." : "Upstream service rejected the request.";
        return new("UpstreamHttp", msg, transient);
    }
}
