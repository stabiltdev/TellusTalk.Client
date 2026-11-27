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

    /// <summary>
    /// Where TellusTalk posts delivery reports, and their secret: the client puts their address in every message, and the
    /// report endpoint takes the reports that carry the secret. <see langword="null"/> for an app that takes none.
    /// </summary>
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

        // The report settings name their own members, which are this class's Reports.
        foreach (var result in Reports.Validate(urlRequired: true))
        {
            yield return new ValidationResult(result.ErrorMessage, [nameof(Reports)]);
        }
    }
}
