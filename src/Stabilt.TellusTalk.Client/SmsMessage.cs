using System.Text.Json;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// An SMS to send through TellusTalk's K8 send API. What TellusTalk's specification forbids is refused as it is set, so
/// a message that can be built is one TellusTalk takes as meant.
/// </summary>
public sealed record SmsMessage
{
    /// <summary>The recipients, each <c>+</c> followed by digits, such as <c>+46701740605</c>.</summary>
    public required IReadOnlyList<string> To
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("To needs at least one recipient.", nameof(value));
            }

            var wrong = value.FirstOrDefault(number => !PhoneNumbers.IsPlusDigits(number));
            if (wrong is not null)
            {
                throw new ArgumentException($"To holds '{wrong}', which is not + followed by digits, such as +46701740605.", nameof(value));
            }

            field = value;
        }
    }

    public required string Text
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>The sender the phone shows, or <see langword="null"/> for the one the TellusTalk account is configured with.</summary>
    public Sender? Sender { get; init; }

    /// <summary>When TellusTalk sends the message, or <see langword="null"/> for at once. It is sent with its offset.</summary>
    public DateTimeOffset? ScheduledSendTime { get; init; }

    /// <summary>Where TellusTalk sends the message's delivery report.</summary>
    public IReadOnlyList<ReportAddress> ReportTo
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    /// <summary>Where TellusTalk sends the recipient's replies, for a <see cref="Sender.ReplyNumber"/> sender.</summary>
    public IReadOnlyList<ReportAddress> ReplyTo
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    /// <summary>The most parts TellusTalk may split the text into, at least 1.</summary>
    public int? MaxParts
    {
        get;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "MaxParts is at least 1.");
            }

            field = value;
        }
    }

    /// <summary>Whether the phone shows the message at once and does not store it.</summary>
    public bool Flash { get; init; }

    /// <summary>How long TellusTalk keeps trying to deliver the message, in whole seconds, at least one.</summary>
    public TimeSpan? ValidityTime
    {
        get;
        init
        {
            if (value < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "ValidityTime is at least a second; TellusTalk counts it in whole seconds.");
            }

            field = value;
        }
    }

    /// <summary>How TellusTalk encodes the text, or <see langword="null"/> for TellusTalk's own choice.</summary>
    public SmsCharacterSet? CharacterSet { get; init; }

    /// <summary>The account's own billing reference for the message, letters and digits only.</summary>
    public string? BillingId
    {
        get;
        init
        {
            if (value is not null && (value.Length == 0 || !value.All(char.IsAsciiLetterOrDigit)))
            {
                throw new ArgumentException($"BillingId is letters a–z and digits only, not '{value}'.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>JSON that TellusTalk carries back unchanged in the message's delivery report.</summary>
    public JsonElement? Echo
    {
        get;
        init
        {
            if (value is { ValueKind: JsonValueKind.Undefined })
            {
                throw new ArgumentException("Echo holds no JSON; a default JsonElement cannot be sent.", nameof(value));
            }

            field = value;
        }
    }
}
