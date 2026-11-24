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
    /// Sends <paramref name="message"/>; the client adds no retries, since a send that failed in transit may have gone
    /// through. When the <c>Reports</c> settings are set, the message's <c>report_to</c> opens with their report address,
    /// read from the current settings so a rotated secret reaches the next message.
    /// </summary>
    /// <exception cref="TellusTalkException">TellusTalk refused the message, or did not answer as a send is answered.</exception>
    public async Task<SendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var reportAddress = ReportAddressOf(options.CurrentValue.Reports);

        // Serialised up front so the request carries its length; JsonContent would stream it chunked.
        using var content = new StringContent(
            JsonSerializer.Serialize(SendRequestBody.From(message, reportAddress), Json.Options),
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

    private static ReportAddress? ReportAddressOf(TellusTalkReportOptions? reports) => reports is null
        ? null
        : ReportAddress.Url(
            reports.Url!,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = ReportAuthorization.HeaderValue(reports.Secret) });

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
