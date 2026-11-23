using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// Reads a report from K8's JSON and writes it back under the same names. Only <c>log_status</c> is required: a field
/// K8 sends in another type or form than was seen is read as missing, so the report still reaches the app.
/// </summary>
internal sealed class DeliveryReportJsonConverter : JsonConverter<DeliveryReport>
{
    private const string SmsPrefix = "sms:";

    public override DeliveryReport Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var body = document.RootElement;
        if (body.ValueKind != JsonValueKind.Object || Text(body, "log_status") is not { } status)
        {
            throw new JsonException("A delivery report is a JSON object with a log_status.");
        }

        return new DeliveryReport
        {
            StatusText = status,
            StatusCode = Number(body, "log_status_code"),
            FailedReason = NonEmptyText(body, "failed_reason"),
            FailedDetails = NonEmptyText(body, "failed_details"),
            To = WithoutSmsPrefix(Text(body, "to_address")),
            JobUuid = Text(body, "job_uuid"),
            LogUuid = Text(body, "log_uuid"),
            Echo = body.TryGetProperty("echo", out var echo) && echo.ValueKind != JsonValueKind.Null ? echo.Clone() : null,
            Parts = Number(body, "quantity"),
            ReportTime = Time(body, "report_time"),
        };
    }

    public override void Write(Utf8JsonWriter writer, DeliveryReport value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("log_status", value.StatusText);
        WriteNumber(writer, "log_status_code", value.StatusCode);
        writer.WriteString("failed_reason", value.FailedReason);
        writer.WriteString("failed_details", value.FailedDetails);
        writer.WriteString("to_address", value.To);
        writer.WriteString("job_uuid", value.JobUuid);
        writer.WriteString("log_uuid", value.LogUuid);
        writer.WritePropertyName("echo");
        if (value.Echo is { } echo)
        {
            echo.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }

        WriteNumber(writer, "quantity", value.Parts);
        if (value.ReportTime is { } reportTime)
        {
            writer.WriteString("report_time", reportTime);
        }
        else
        {
            writer.WriteNull("report_time");
        }

        writer.WriteEndObject();
    }

    private static JsonElement? Field(JsonElement body, string name, JsonValueKind kind) =>
        body.TryGetProperty(name, out var field) && field.ValueKind == kind ? field : null;

    private static string? Text(JsonElement body, string name) => Field(body, name, JsonValueKind.String)?.GetString();

    private static string? NonEmptyText(JsonElement body, string name) => Text(body, name) is { Length: > 0 } text ? text : null;

    private static int? Number(JsonElement body, string name) =>
        Field(body, name, JsonValueKind.Number) is { } number && number.TryGetInt32(out var value) ? value : null;

    private static DateTimeOffset? Time(JsonElement body, string name)
    {
        if (Field(body, name, JsonValueKind.String) is not { } text || !text.TryGetDateTime(out var time))
        {
            return null;
        }

        // K8's report times are UTC; one written without an offset would otherwise be read in the server's time zone.
        return time.Kind == DateTimeKind.Unspecified ? new DateTimeOffset(time, TimeSpan.Zero) : text.GetDateTimeOffset();
    }

    // The client writes a number as sms:<number>, so the report gives back the number the message held.
    private static string? WithoutSmsPrefix(string? address) =>
        address is not null && address.StartsWith(SmsPrefix, StringComparison.Ordinal) ? address[SmsPrefix.Length..] : address;

    private static void WriteNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
