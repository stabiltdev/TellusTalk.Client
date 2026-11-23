using System.ComponentModel.DataAnnotations;

namespace Stabilt.TellusTalk.Client;

/// <summary>Where TellusTalk is and the account to send as.</summary>
public sealed class TellusTalkOptions : IValidatableObject
{
    [Required]
    public Uri BaseUrl { get; set; } = new("https://k8api.tellustalk.com/");

    [Required]
    public string UserName { get; set; } = string.Empty;

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

        if (Reports.PreviousSecrets.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult("Reports:PreviousSecrets holds an empty secret.", [nameof(Reports)]);
        }
    }
}
