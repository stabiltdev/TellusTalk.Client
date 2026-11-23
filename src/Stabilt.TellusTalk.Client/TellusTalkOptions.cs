using System.ComponentModel.DataAnnotations;

namespace Stabilt.TellusTalk.Client;

/// <summary>Where TellusTalk is and the account to send as.</summary>
public sealed class TellusTalkOptions : IValidatableObject
{
    /// <summary>
    /// TellusTalk's K8 API, an absolute http or https URL; <c>https://k8api.tellustalk.com/</c> unless set, such as to an
    /// emulator's.
    /// </summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://k8api.tellustalk.com/");

    /// <summary>The TellusTalk account's user name, which the client sends with Basic authentication.</summary>
    [Required]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// The account's password. Read from a configuration section, a changed password reaches the next client created,
    /// without a restart.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Where TellusTalk posts delivery reports, and their secret; <see langword="null"/> for an app that takes none.</summary>
    public TellusTalkReportOptions? Reports { get; set; }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (BaseUrl is { IsAbsoluteUri: false } or { Scheme: not ("http" or "https") })
        {
            yield return new ValidationResult(
                $"BaseUrl is an absolute http or https URL, such as https://k8api.tellustalk.com/, not '{BaseUrl}'.",
                [nameof(BaseUrl)]);
        }

        if (Reports is null)
        {
            yield break;
        }

        if (Reports.Url is not { IsAbsoluteUri: true, Scheme: "http" or "https" })
        {
            yield return new ValidationResult(
                $"Reports:Url is an absolute http or https URL, such as https://app.example/sms/reports, not '{Reports.Url}'.",
                [nameof(Reports)]);
        }

        if (string.IsNullOrWhiteSpace(Reports.Secret))
        {
            yield return new ValidationResult(
                "Reports:Secret is required: TellusTalk's reports carry it, and the endpoint turns away a report without it.",
                [nameof(Reports)]);
        }
        else if (!IsVisibleAscii(Reports.Secret))
        {
            yield return new ValidationResult(
                "Reports:Secret is visible ASCII without spaces, such as a base64 or hex string, since TellusTalk carries it back in a header.",
                [nameof(Reports)]);
        }

        if (Reports.PreviousSecrets.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult("Reports:PreviousSecrets holds an empty secret.", [nameof(Reports)]);
        }
        else if (!Reports.PreviousSecrets.All(IsVisibleAscii))
        {
            yield return new ValidationResult(
                "Reports:PreviousSecrets holds a secret that is not visible ASCII without spaces.",
                [nameof(Reports)]);
        }
    }

    // A header loses a secret's leading and trailing spaces and cannot carry a control character, and how K8 encodes a
    // character beyond ASCII has not been seen, so such a secret would turn away every report.
    private static bool IsVisibleAscii(string secret) => secret.All(character => character is > ' ' and < '\x7f');
}
