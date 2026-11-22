using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

/// <summary>An address TellusTalk could not use.</summary>
/// <param name="Address">The address as TellusTalk was sent it, such as <c>sms:+46701740605</c>.</param>
/// <param name="Error">TellusTalk's reason, such as <c>Number Not Numeric</c> or <c>Unknown Address Type</c>.</param>
public sealed record InvalidAddress(
    [property: JsonPropertyName("address"), JsonRequired] string Address,
    [property: JsonPropertyName("error"), JsonRequired] string Error);
