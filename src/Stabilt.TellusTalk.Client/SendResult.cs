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
    [property: JsonPropertyName("job_size"), JsonRequired] int JobSize);
