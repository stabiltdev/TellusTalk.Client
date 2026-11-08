namespace Stabilt.TellusTalk.Client;

internal static class PhoneNumbers
{
    /// <summary>The specification's form of a number: <c>+</c> followed by digits only.</summary>
    public static bool IsPlusDigits(string? value) =>
        value is { Length: > 1 } && value[0] == '+' && !value.AsSpan(1).ContainsAnyExceptInRange('0', '9');
}
