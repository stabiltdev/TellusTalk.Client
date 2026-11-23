namespace Stabilt.TellusTalk.Client;

/// <summary>Where TellusTalk posts delivery reports, and the secret that shows a report comes from TellusTalk.</summary>
public sealed class TellusTalkReportOptions
{
    /// <summary>The http or https URL TellusTalk posts each report to, as the world reaches the app.</summary>
    public Uri? Url { get; set; }

    /// <summary>The secret the report address carries in <c>Authorization</c>, and a report must carry back.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Secrets a report is still taken with. A report carries the secret its message was sent with, so a rotated secret
    /// stays listed until the reports of messages sent with it have come.
    /// </summary>
    public IList<string> PreviousSecrets { get; } = [];
}
