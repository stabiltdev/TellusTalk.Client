using System.ComponentModel.DataAnnotations;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// Where TellusTalk posts delivery reports, and the secret that shows a report comes from TellusTalk. A set
/// <see cref="Url"/> is checked here, and a missing one is the sender's to require.
/// </summary>
public sealed class TellusTalkReportOptions : IValidatableObject
{
    /// <summary>
    /// The http or https URL TellusTalk posts each report to, as the world reaches the app. Every message carries it, and
    /// <c>MapTellusTalkReports()</c> maps the report endpoint at its path.
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>The secret the report address carries in <c>Authorization</c>, and a report must carry back.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Secrets a report is still taken with. A report carries the secret its message was sent with, so a rotated secret
    /// stays listed until the reports of messages sent with it have come.
    /// </summary>
    public IList<string> PreviousSecrets { get; } = [];

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext) =>
        Validate(urlRequired: false);

    internal IEnumerable<ValidationResult> Validate(bool urlRequired)
    {
        if ((urlRequired || Url is not null) && Url is not { IsAbsoluteUri: true, Scheme: "http" or "https" })
        {
            yield return new ValidationResult(
                $"Reports:Url is an absolute http or https URL, such as https://app.example/sms/reports, not '{Url}'.",
                [nameof(Url)]);
        }

        if (string.IsNullOrWhiteSpace(Secret))
        {
            yield return new ValidationResult(
                "Reports:Secret is required: TellusTalk's reports carry it, and the endpoint turns away a report without it.",
                [nameof(Secret)]);
        }
        else if (!IsVisibleAscii(Secret))
        {
            yield return new ValidationResult(
                "Reports:Secret is visible ASCII without spaces, such as a base64 or hex string, since TellusTalk carries it back in a header.",
                [nameof(Secret)]);
        }

        if (PreviousSecrets.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult("Reports:PreviousSecrets holds an empty secret.", [nameof(PreviousSecrets)]);
        }
        else if (!PreviousSecrets.All(IsVisibleAscii))
        {
            yield return new ValidationResult(
                "Reports:PreviousSecrets holds a secret that is not visible ASCII without spaces.",
                [nameof(PreviousSecrets)]);
        }
    }

    // A header loses a secret's leading and trailing spaces and cannot carry a control character, and how K8 encodes a
    // character beyond ASCII has not been seen, so such a secret would turn away every report.
    private static bool IsVisibleAscii(string secret) => secret.All(character => character is > ' ' and < '\x7f');
}
