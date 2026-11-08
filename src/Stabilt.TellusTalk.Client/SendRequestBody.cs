using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

/// <summary>A send request as TellusTalk reads it; a field left null is left out.</summary>
internal sealed record SendRequestBody(
    [property: JsonPropertyName("to")] IReadOnlyList<string> To,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("sms_originator_source")] string? OriginatorSource,
    [property: JsonPropertyName("sms_originator_text")] string? OriginatorText,
    [property: JsonPropertyName("sms_originator_number")] string? OriginatorNumber,
    [property: JsonPropertyName("sms_single_thread")] bool? SingleThread,
    [property: JsonPropertyName("scheduled_send_time")] string? ScheduledSendTime,
    [property: JsonPropertyName("report_to")] IReadOnlyList<string>? ReportTo,
    [property: JsonPropertyName("reply_to")] IReadOnlyList<string>? ReplyTo,
    [property: JsonPropertyName("sms_max_parts")] int? MaxParts,
    [property: JsonPropertyName("sms_flash")] bool? Flash,
    [property: JsonPropertyName("sms_validity_time")] int? ValidityTime,
    [property: JsonPropertyName("sms_character_set")] string? CharacterSet,
    [property: JsonPropertyName("billing_id")] string? BillingId,
    [property: JsonPropertyName("echo")] JsonElement? Echo)
{
    public static SendRequestBody From(SmsMessage message) => new(
        message.To
            .Select(number => "sms:" + number)
            .ToList(),
        message.Text,
        message.Sender?.Source,
        message.Sender?.OriginatorText,
        message.Sender?.OriginatorNumber,
        message.Sender?.SingleThread,
        message.ScheduledSendTime?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
        Addresses(message.ReportTo),
        Addresses(message.ReplyTo),
        message.MaxParts,
        message.Flash ? true : null,
        (int?)message.ValidityTime?.TotalSeconds,
        message.CharacterSet switch
        {
            null => null,
            SmsCharacterSet.Optimize => "optimize",
            SmsCharacterSet.Gsm => "gsm",
            SmsCharacterSet.Unicode => "unicode",
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.CharacterSet, "An unknown character set."),
        },
        message.BillingId,
        message.Echo);

    private static List<string>? Addresses(IReadOnlyList<ReportAddress> addresses) => addresses.Count == 0
        ? null
        : addresses
            .Select(address => address.Value)
            .ToList();
}
