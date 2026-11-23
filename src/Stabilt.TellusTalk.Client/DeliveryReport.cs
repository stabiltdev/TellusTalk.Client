using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// A delivery report TellusTalk posts to a report URL, read from its JSON with <see cref="JsonSerializer"/>. The message's
/// text, subject and html are left out on purpose: the app has them already, and a report carries personal data enough.
/// </summary>
[JsonConverter(typeof(DeliveryReportJsonConverter))]
public sealed record DeliveryReport
{
    /// <summary>TellusTalk's status as it came, such as <c>txtDELIVERED</c>, from <c>log_status</c>.</summary>
    public required string StatusText { get; init; }

    public DeliveryStatus Status => StatusText switch
    {
        "txtDELIVERED" => DeliveryStatus.Delivered,
        "txtDELIVERY_FAILED" => DeliveryStatus.Failed,
        "txtBLOCKED" => DeliveryStatus.Blocked,
        _ => DeliveryStatus.Unknown,
    };

    /// <summary>TellusTalk's code for the status, such as 22 for delivered, from <c>log_status_code</c>.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Why the SMS was not delivered, such as <c>txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None</c>.</summary>
    public string? FailedReason { get; init; }

    /// <summary>More on why, such as the message a held-back send duplicates.</summary>
    public string? FailedDetails { get; init; }

    /// <summary>
    /// The recipient's number as the message gave it, such as <c>+46701740605</c>; an address that is not a number is
    /// kept as TellusTalk wrote it.
    /// </summary>
    public string? To { get; init; }

    /// <summary>The send's job, as in <see cref="SendResult.JobUuid"/>.</summary>
    public string? JobUuid { get; init; }

    /// <summary>The recipient's log id, as in <see cref="SendResult.LogUuid"/> for a send to one recipient.</summary>
    public string? LogUuid { get; init; }

    /// <summary>The message's <see cref="SmsMessage.Echo"/>, as it was sent.</summary>
    public JsonElement? Echo { get; init; }

    /// <summary>How many parts the SMS was sent in, from <c>quantity</c>.</summary>
    public int? Parts { get; init; }

    /// <summary>When TellusTalk learned the outcome; <see langword="null"/> for a send held back at once.</summary>
    public DateTimeOffset? ReportTime { get; init; }
}
