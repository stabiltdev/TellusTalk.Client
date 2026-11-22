using System.Runtime.CompilerServices;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// The sender an SMS recipient's phone shows, TellusTalk's <c>sms_originator_source</c> and the fields that go with it: a
/// name, a number, or a number of TellusTalk's that takes replies.
/// </summary>
public sealed class SmsSender
{
    /// <summary>The longest name a phone shows as a sender.</summary>
    private const int NameLimit = 11;

    private SmsSender(
        string source,
        string? originatorText = null,
        string? originatorNumber = null,
        string? subjectText = null,
        IReadOnlyList<ReportAddress>? replyTo = null,
        bool? singleThread = null)
    {
        Source = source;
        OriginatorText = originatorText;
        OriginatorNumber = originatorNumber;
        SubjectText = subjectText;
        ReplyTo = replyTo ?? [];
        SingleThread = singleThread;
    }

    internal string Source { get; }

    internal string? OriginatorText { get; }

    internal string? OriginatorNumber { get; }

    internal string? SubjectText { get; }

    internal IReadOnlyList<ReportAddress> ReplyTo { get; }

    internal bool? SingleThread { get; }

    /// <summary>A name of at most 11 characters, such as <c>Clinic</c>. The recipient cannot reply to it.</summary>
    public static SmsSender Name(string name)
    {
        RequireShownName(name);

        return new SmsSender("text", originatorText: name);
    }

    /// <summary>
    /// A name of at most 11 characters sent as the message's subject, which the phone shows as it shows a
    /// <see cref="Name"/>. Unlike a name, a subject sets a message apart from the duplicates TellusTalk holds back, the
    /// same text to the same number within five minutes, and it starts the delivery report's text.
    /// </summary>
    public static SmsSender Subject(string subject)
    {
        RequireShownName(subject);

        return new SmsSender("subject", subjectText: subject);
    }

    /// <summary>A number, <c>+</c> followed by digits, such as <c>+46701740605</c>.</summary>
    public static SmsSender Number(string number)
    {
        if (!PhoneNumbers.IsPlusDigits(number))
        {
            throw new ArgumentException($"A sender number is + followed by digits, such as +46701740605, not '{number}'.", nameof(number));
        }

        return new SmsSender("number", originatorNumber: number);
    }

    /// <summary>A number from TellusTalk's pool, so the recipient can reply.</summary>
    /// <param name="replyTo">Where TellusTalk sends the recipient's replies.</param>
    /// <param name="samePerRecipient">
    /// Whether a recipient gets the number earlier messages to them came from, rather than a new one, which keeps each
    /// conversation's replies apart.
    /// </param>
    public static SmsSender ReplyNumber(IReadOnlyList<ReportAddress>? replyTo = null, bool samePerRecipient = false)
    {
        if (replyTo?.Contains(null!) == true)
        {
            throw new ArgumentException("replyTo holds a null address.", nameof(replyTo));
        }

        // A copy, so a list the caller changes afterwards cannot take back what was checked.
        return new SmsSender(
            "replysms",
            replyTo: replyTo is null ? null : [.. replyTo],
            singleThread: samePerRecipient ? true : null);
    }

    private static void RequireShownName(string name, [CallerArgumentExpression(nameof(name))] string? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameter);
        if (name.Length > NameLimit)
        {
            throw new ArgumentException($"A sender name is at most {NameLimit} characters, not {name.Length}: '{name}'.", parameter);
        }
    }
}
