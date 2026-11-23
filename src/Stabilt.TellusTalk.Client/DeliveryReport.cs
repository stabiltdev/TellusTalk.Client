using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// A delivery report TellusTalk posts to a report URL, read from its JSON with <see cref="JsonSerializer"/>. The message's
/// text, subject and html are left out on purpose: the app has them already, and a report carries personal data enough.
/// </summary>
/// <remarks>
/// Two reports are equal when their properties are, <see cref="Echo"/> compared as JSON. Written as text, a report leaves
/// out <see cref="To"/> and <see cref="Echo"/>.
/// </remarks>
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

    // A JsonElement equals only itself, so the record's own equality would find no two reports with an Echo equal.
    public bool Equals(DeliveryReport? other) =>
        other is not null
        && string.Equals(StatusText, other.StatusText, StringComparison.Ordinal)
        && StatusCode == other.StatusCode
        && string.Equals(FailedReason, other.FailedReason, StringComparison.Ordinal)
        && string.Equals(FailedDetails, other.FailedDetails, StringComparison.Ordinal)
        && string.Equals(To, other.To, StringComparison.Ordinal)
        && string.Equals(JobUuid, other.JobUuid, StringComparison.Ordinal)
        && string.Equals(LogUuid, other.LogUuid, StringComparison.Ordinal)
        && Parts == other.Parts
        && ReportTime == other.ReportTime
        && (Echo, other.Echo) switch
        {
            (null, null) => true,
            ({ } echo, { } otherEcho) => JsonElement.DeepEquals(echo, otherEcho),
            _ => false,
        };

    // Echo is left out: a hash that agrees with DeepEquals would have to walk the JSON, and the ids set reports apart.
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StatusText, StringComparer.Ordinal);
        hash.Add(StatusCode);
        hash.Add(FailedReason, StringComparer.Ordinal);
        hash.Add(FailedDetails, StringComparer.Ordinal);
        hash.Add(To, StringComparer.Ordinal);
        hash.Add(JobUuid, StringComparer.Ordinal);
        hash.Add(LogUuid, StringComparer.Ordinal);
        hash.Add(Parts);
        hash.Add(ReportTime);

        return hash.ToHashCode();
    }

    // To and Echo are left out, so a report written to a log carries neither the recipient's number nor the app's data.
    private bool PrintMembers(StringBuilder builder)
    {
        builder
            .Append(CultureInfo.InvariantCulture, $"StatusText = {StatusText}, Status = {Status}, StatusCode = {StatusCode}, ")
            .Append(CultureInfo.InvariantCulture, $"FailedReason = {FailedReason}, FailedDetails = {FailedDetails}, ")
            .Append(CultureInfo.InvariantCulture, $"JobUuid = {JobUuid}, LogUuid = {LogUuid}, Parts = {Parts}, ReportTime = {ReportTime}");

        return true;
    }
}
