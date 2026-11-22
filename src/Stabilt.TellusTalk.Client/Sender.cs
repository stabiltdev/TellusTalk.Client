using System.Runtime.CompilerServices;

namespace Stabilt.TellusTalk.Client;

/// <summary>The sender a recipient's phone shows: a name, a number, or a number of TellusTalk's that takes replies.</summary>
public sealed class Sender
{
    /// <summary>The longest name a phone shows as a sender.</summary>
    private const int NameLimit = 11;

    private Sender(
        string source,
        string? originatorText = null,
        string? originatorNumber = null,
        string? subjectText = null,
        bool? singleThread = null)
    {
        Source = source;
        OriginatorText = originatorText;
        OriginatorNumber = originatorNumber;
        SubjectText = subjectText;
        SingleThread = singleThread;
    }

    internal string Source { get; }

    internal string? OriginatorText { get; }

    internal string? OriginatorNumber { get; }

    internal string? SubjectText { get; }

    internal bool? SingleThread { get; }

    /// <summary>A name of at most 11 characters, such as <c>Clinic</c>. The recipient cannot reply to it.</summary>
    public static Sender Name(string name)
    {
        RequireShownName(name);

        return new Sender("text", originatorText: name);
    }

    /// <summary>
    /// A name of at most 11 characters sent as the message's subject, which the phone shows as it shows a
    /// <see cref="Name"/>. Unlike a name, a subject sets a message apart from the duplicates TellusTalk holds back, the
    /// same text to the same number within five minutes, and it starts the delivery report's text.
    /// </summary>
    public static Sender Subject(string subject)
    {
        RequireShownName(subject);

        return new Sender("subject", subjectText: subject);
    }

    /// <summary>A number, <c>+</c> followed by digits, such as <c>+46701740605</c>.</summary>
    public static Sender Number(string number)
    {
        if (!PhoneNumbers.IsPlusDigits(number))
        {
            throw new ArgumentException($"A sender number is + followed by digits, such as +46701740605, not '{number}'.", nameof(number));
        }

        return new Sender("number", originatorNumber: number);
    }

    /// <summary>A number from TellusTalk's pool, so the recipient can reply; replies go to the message's <c>ReplyTo</c>.</summary>
    /// <param name="samePerRecipient">
    /// Whether a recipient gets the number earlier messages to them came from, rather than a new one, which keeps each
    /// conversation's replies apart.
    /// </param>
    public static Sender ReplyNumber(bool samePerRecipient = false) =>
        new("replysms", singleThread: samePerRecipient ? true : null);

    private static void RequireShownName(string name, [CallerArgumentExpression(nameof(name))] string? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameter);
        if (name.Length > NameLimit)
        {
            throw new ArgumentException($"A sender name is at most {NameLimit} characters, not {name.Length}: '{name}'.", parameter);
        }
    }
}
