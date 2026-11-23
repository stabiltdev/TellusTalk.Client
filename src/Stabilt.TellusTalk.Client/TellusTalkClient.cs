using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// Sends SMS through TellusTalk's K8 send API. Register it with <c>AddTellusTalkClient</c>, which gives it TellusTalk's
/// address and the account's credentials.
/// </summary>
public sealed class TellusTalkClient(HttpClient httpClient, IOptionsMonitor<TellusTalkOptions> options)
{
    private static readonly Uri SendPath = new("send/v1", UriKind.Relative);

    /// <summary>
    /// The address for a message's <c>ReportTo</c>: the <c>Reports</c> URL, carrying the report secret in
    /// <c>Authorization</c>. It is read from the current settings, so a rotated secret is in the next address.
    /// </summary>
    /// <exception cref="InvalidOperationException">The settings have no <c>Reports</c>.</exception>
    public ReportAddress ReportAddress
    {
        get
        {
            var reports = options.CurrentValue.Reports
                ?? throw new InvalidOperationException("The report address needs the Reports settings, Reports:Url and Reports:Secret, beside the client's.");

            return ReportAddress.Url(
                reports.Url!,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = ReportAuthorization.HeaderValue(reports.Secret) });
        }
    }

    /// <summary>Sends <paramref name="message"/>; the client adds no retries, since a send that failed in transit may have gone through.</summary>
    /// <exception cref="TellusTalkException">TellusTalk refused the message, or did not answer as a send is answered.</exception>
    public async Task<SendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Serialised up front so the request carries its length; JsonContent would stream it chunked.
        using var content = new StringContent(
            JsonSerializer.Serialize(SendRequestBody.From(message), Json.Options),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient
            .PostAsync(SendPath, content, cancellationToken)
            .ConfigureAwait(false);

        var text = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new TellusTalkException(response.StatusCode, text);
        }

        return Read(response.StatusCode, text);
    }

    private static SendResult Read(HttpStatusCode statusCode, string text)
    {
        try
        {
            return JsonSerializer.Deserialize<SendResult>(text)
                ?? throw new TellusTalkException(statusCode, text, new JsonException("The answer is JSON null."));
        }
        catch (JsonException exception)
        {
            throw new TellusTalkException(statusCode, text, exception);
        }
    }
}
