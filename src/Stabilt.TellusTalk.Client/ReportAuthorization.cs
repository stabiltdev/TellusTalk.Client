using System.Security.Cryptography;
using System.Text;

namespace Stabilt.TellusTalk.Client;

/// <summary>
/// The <c>Authorization</c> scheme a report address carries its secret in, <c>Report &lt;secret&gt;</c>. A scheme of its
/// own rather than <c>Bearer</c>, which an app's JWT authentication would try to read as a token.
/// </summary>
public static class ReportAuthorization
{
    public const string Scheme = "Report";

    /// <summary>
    /// Whether <paramref name="authorization"/>, a request's <c>Authorization</c>, carries the secret of
    /// <paramref name="reports"/> or one of its previous secrets in the <c>Report</c> scheme.
    /// </summary>
    public static bool IsValid(string? authorization, TellusTalkReportOptions reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var separator = authorization?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        if (separator < 0 || !string.Equals(authorization![..separator], Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var given = Hash(authorization[(separator + 1)..]);

        // Every candidate is compared, and through its hash, so the time taken says neither which secret matched nor
        // how much of one did.
        var valid = false;
        foreach (var secret in reports.PreviousSecrets.Prepend(reports.Secret))
        {
            valid |= secret.Length > 0 && CryptographicOperations.FixedTimeEquals(given, Hash(secret));
        }

        return valid;
    }

    internal static string HeaderValue(string secret) => $"{Scheme} {secret}";

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
