using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Stabilt.TellusTalk.Client;

/// <summary>TellusTalk refused a send, or answered with something other than a send's answer.</summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only the client throws it, always with TellusTalk's answer.")]
[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors", Justification = "Only the client throws it, always with TellusTalk's answer.")]
public sealed class TellusTalkException : Exception
{
    /// <summary>Enough of an answer to tell an error page from an error text, without a whole page in a log.</summary>
    private const int ShownLength = 500;

    internal TellusTalkException(HttpStatusCode statusCode, string responseText, Exception? innerException = null)
        : base(Describe(statusCode, responseText, innerException is not null), innerException)
    {
        StatusCode = statusCode;
        ResponseText = responseText;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary>TellusTalk's answer as it came.</summary>
    public string ResponseText { get; }

    private static string Describe(HttpStatusCode statusCode, string responseText, bool unreadable)
    {
        var shown = responseText.Length <= ShownLength ? responseText : responseText[..ShownLength] + "…";
        var answered = $"TellusTalk answered {(int)statusCode} {statusCode}";

        return unreadable ? $"{answered} with no send answer: {shown}" : $"{answered}: {shown}";
    }
}
