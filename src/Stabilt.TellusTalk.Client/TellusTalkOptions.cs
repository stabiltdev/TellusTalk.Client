using System.ComponentModel.DataAnnotations;

namespace Stabilt.TellusTalk.Client;

/// <summary>Where TellusTalk is and the account to send as, read from the <c>TellusTalk</c> section.</summary>
public sealed class TellusTalkOptions : IValidatableObject
{
    public const string SectionName = "TellusTalk";

    /// <summary>TellusTalk's K8 API, or an emulator's address in its place.</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://k8api.tellustalk.com/");

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (BaseUrl is { IsAbsoluteUri: false } or { Scheme: not ("http" or "https") })
        {
            yield return new ValidationResult(
                $"BaseUrl is an absolute http or https URL, such as https://k8api.tellustalk.com/, not '{BaseUrl}'.",
                [nameof(BaseUrl)]);
        }
    }
}
