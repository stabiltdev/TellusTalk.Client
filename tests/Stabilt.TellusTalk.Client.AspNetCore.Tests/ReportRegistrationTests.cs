using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client.AspNetCore.Tests.Support;
using Stabilt.TellusTalk.Client.Tests;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests;

public sealed class ReportRegistrationTests
{
    private static readonly IReadOnlyDictionary<string, string?> SecretOnly = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["TellusTalk:Reports:Secret"] = "s3cret",
    };

    [Fact]
    public async Task An_app_with_only_the_report_secret_takes_reports_at_its_route()
    {
        await using var app = await StartAsync(
            SecretOnly,
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration));

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        app.Services.GetRequiredService<ReceivedReports>().Reports.Should().ContainSingle();
    }

    [Fact]
    public async Task An_app_with_only_the_report_secret_turns_away_a_report_without_it()
    {
        await using var app = await StartAsync(
            SecretOnly,
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration));

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered, "Report wrong");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        app.Services.GetRequiredService<ReceivedReports>().Reports.Should().BeEmpty();
    }

    [Fact]
    public async Task A_section_of_the_apps_own_name_is_read_under_the_configuration()
    {
        await using var app = await StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["Sms:Reports:Secret"] = "s3cret" },
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration, "Sms"));

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_section_passed_alone_is_read_as_the_TellusTalk_section()
    {
        await using var app = await StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["Sms:Reports:Secret"] = "s3cret" },
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration.GetSection("Sms")));

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Code_alone_configures_the_reports()
    {
        await using var app = await StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal),
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(reports => reports.Secret = "coded"));

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered, "Report coded");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_app_that_also_sends_takes_reports_with_the_secret_of_its_one_section()
    {
        await using var app = await StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TellusTalk:UserName"] = "tester",
                ["TellusTalk:Password"] = "secret",
                ["TellusTalk:Reports:Url"] = "https://app.invalid/sms/reports",
                ["TellusTalk:Reports:Secret"] = "s3cret",
            },
            builder =>
            {
                builder.Services.AddTellusTalkClient(builder.Configuration);
                builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration);
            });

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public Task Passing_the_Reports_section_rather_than_its_parent_fails_when_the_app_starts()
    {
        var start = () => StartAsync(
            SecretOnly,
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration.GetSection("TellusTalk:Reports")));

        return start.Should().ThrowAsync<OptionsValidationException>().WithMessage("*Reports:Secret*");
    }

    [Theory]
    [InlineData("TellusTalk:Reports:Url", "/sms/reports")]
    [InlineData("TellusTalk:Reports:Url", "ftp://app.invalid/sms/reports")]
    [InlineData("TellusTalk:Reports:Secret", " ")]
    [InlineData("TellusTalk:Reports:Secret", "s3c ret")]
    [InlineData("TellusTalk:Reports:PreviousSecrets:0", "")]
    [InlineData("TellusTalk:Reports:PreviousSecrets:0", "0ld ")]
    public Task Report_settings_that_break_a_rule_fail_when_the_app_starts(string key, string value)
    {
        var settings = new Dictionary<string, string?>(SecretOnly, StringComparer.Ordinal) { [key] = value };

        var start = () => StartAsync(
            settings,
            builder => builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration));

        return start.Should().ThrowAsync<OptionsValidationException>().WithMessage("*Reports:*");
    }

    /// <summary>Starts an app on <see cref="TestServer"/> with <paramref name="settings"/>, registered by <paramref name="register"/>.</summary>
    private static async Task<WebApplication> StartAsync(
        IReadOnlyDictionary<string, string?> settings,
        Action<WebApplicationBuilder> register)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        register(builder);
        builder.Services.AddSingleton<ReceivedReports>();

        var app = builder.Build();
        try
        {
            app.MapTellusTalkReports("/sms/reports");
            await app.StartAsync(TestContext.Current.CancellationToken);

            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }
}
