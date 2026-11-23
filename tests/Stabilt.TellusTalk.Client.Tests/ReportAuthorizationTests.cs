namespace Stabilt.TellusTalk.Client.Tests;

public sealed class ReportAuthorizationTests
{
    private static readonly TellusTalkReportOptions Reports = new()
    {
        Url = new Uri("https://app.invalid/sms/reports"),
        Secret = "s3cret",
        PreviousSecrets = { "0ld" },
    };

    [Theory]
    [InlineData("Report s3cret")]
    [InlineData("report s3cret")]
    [InlineData("REPORT s3cret")]
    [InlineData("Report 0ld")]
    public void A_secret_in_the_report_scheme_is_valid(string authorization) =>
        ReportAuthorization.IsValid(authorization, Reports).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("s3cret")]
    [InlineData("Report")]
    [InlineData("Report ")]
    [InlineData("Bearer s3cret")]
    [InlineData("Report s3cre")]
    [InlineData("Report s3cret2")]
    [InlineData("Report  s3cret")]
    public void Anything_else_is_not_valid(string? authorization) =>
        ReportAuthorization.IsValid(authorization, Reports).Should().BeFalse();
}
