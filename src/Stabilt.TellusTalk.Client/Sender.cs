namespace Stabilt.TellusTalk.Client;

/// <summary>The sender a recipient's phone shows: a name, a number, or a number of TellusTalk's that takes replies.</summary>
public sealed class Sender
{
    /// <summary>The longest name a phone shows as a sender.</summary>
    private const int NameLimit = 11;

    private Sender(string source, string? originatorText = null, string? originatorNumber = null, bool? singleThread = null)
    {
        Source = source;
        OriginatorText = originatorText;
        OriginatorNumber = originatorNumber;
        SingleThread = singleThread;
    }

    internal string Source { get; }

    internal string? OriginatorText { get; }

    internal string? OriginatorNumber { get; }

    internal bool? SingleThread { get; }

    /// <summary>A name of at most 11 characters, such as <c>Clinic</c>. The recipient cannot reply to it.</summary>
    public static Sender Name(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > NameLimit)
        {
            throw new ArgumentException($"A sender name is at most {NameLimit} characters, not {name.Length}: '{name}'.", nameof(name));
        }

        return new Sender("text", originatorText: name);
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
}
