using System.Net.Mail;
using System.Text.Json;

namespace Stabilt.TellusTalk.Client;

/// <summary>Where TellusTalk sends a delivery report or a reply: a URL, an e-mail address or a number.</summary>
public sealed class ReportAddress
{
    private ReportAddress(string value) => Value = value;

    /// <summary>The address in TellusTalk's form; it can hold the headers a report request carries.</summary>
    internal string Value { get; }

    /// <summary>An http or https URL that TellusTalk posts the report to as JSON.</summary>
    public static ReportAddress Url(Uri url) => new(HttpUrl(url).AbsoluteUri);

    /// <summary>
    /// An http or https URL that TellusTalk posts the report to as JSON, with <paramref name="headers"/> on the request,
    /// such as an <c>Authorization</c> the receiver checks.
    /// </summary>
    public static ReportAddress Url(Uri url, IReadOnlyDictionary<string, string> headers)
    {
        var target = HttpUrl(url);
        ArgumentNullException.ThrowIfNull(headers);

        // TellusTalk takes an address's settings as JSON between its scheme and its "://".
        var settings = JsonSerializer.Serialize(new { headers }, Json.Options);

        return new($"{target.Scheme}:{settings}://{target.AbsoluteUri[(target.Scheme.Length + "://".Length)..]}");
    }

    /// <summary>An e-mail address, such as <c>reports@example.se</c>, without a display name.</summary>
    public static ReportAddress Email(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (!MailAddress.TryCreate(address, out var parsed) || !string.Equals(parsed.Address, address, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{address}' is not an e-mail address alone.", nameof(address));
        }

        return new("email:" + address);
    }

    /// <summary>A number that TellusTalk sends the report to by SMS, <c>+</c> followed by digits.</summary>
    public static ReportAddress Sms(string number)
    {
        if (!PhoneNumbers.IsPlusDigits(number))
        {
            throw new ArgumentException($"A number is + followed by digits, such as +46701740605, not '{number}'.", nameof(number));
        }

        return new("sms:" + number);
    }

    private static Uri HttpUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url is not { IsAbsoluteUri: true, Scheme: "http" or "https" })
        {
            throw new ArgumentException($"A report URL is an absolute http or https URL, not '{url}'.", nameof(url));
        }

        return url;
    }
}
