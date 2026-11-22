using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

/// <summary>TellusTalk's answer to a send, under TellusTalk's names; delivery reports carry the same ids.</summary>
/// <param name="LogUuid">
/// The recipient's log id for a send to one recipient, or <see langword="null"/> for several, whose delivery reports each
/// carry their own.
/// </param>
/// <param name="JobSize">How many recipients the job sends to.</param>
public sealed record SendResult(
    [property: JsonPropertyName("job_id"), JsonRequired] string JobId,
    [property: JsonPropertyName("job_uuid"), JsonRequired] string JobUuid,
    [property: JsonPropertyName("log_uuid")] string? LogUuid,
    [property: JsonPropertyName("job_size"), JsonRequired] int JobSize)
{
    /// <summary>Recipients TellusTalk blocks rather than sends to; they still count in <see cref="JobSize"/>.</summary>
    [JsonPropertyName("invalid_recipients")]
    public IReadOnlyList<InvalidAddress> InvalidRecipients { get; init; } = [];

    /// <summary>Report addresses TellusTalk cannot use; the message is sent all the same.</summary>
    [JsonPropertyName("invalid_report_to")]
    public IReadOnlyList<InvalidAddress> InvalidReportTo { get; init; } = [];

    /// <summary>Reply addresses TellusTalk cannot use; the message is sent all the same.</summary>
    [JsonPropertyName("invalid_reply_to")]
    public IReadOnlyList<InvalidAddress> InvalidReplyTo { get; init; } = [];
}
