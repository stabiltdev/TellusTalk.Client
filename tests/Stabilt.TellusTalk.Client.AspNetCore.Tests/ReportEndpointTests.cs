using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client.AspNetCore.Tests.Support;
using Stabilt.TellusTalk.Client.Tests;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests;

public sealed class ReportEndpointTests
{
    [Fact]
    public async Task A_report_with_the_secret_reaches_the_handler_and_is_answered_no_content()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Received(app).Should().ContainSingle().Which.To.Should().Be("+46701740605");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer s3cret")]
    [InlineData("Report wrong")]
    [InlineData("Report")]
    public async Task A_report_without_the_secret_is_turned_away_unhandled(string? authorization)
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered, authorization);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Received(app).Should().BeEmpty();
    }

    [Fact]
    public async Task A_request_without_the_secret_is_turned_away_before_its_body_is_read()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(app, "not json", authorization: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Received(app).Should().BeEmpty();
    }

    [Fact]
    public async Task A_report_with_a_previous_secret_is_taken()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered, "Report 0ld");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Received(app).Should().ContainSingle();
    }

    [Fact]
    public async Task A_secret_rotated_in_configuration_is_taken_without_a_restart()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        app.Configuration["TellusTalk:Reports:Secret"] = "rotated";
        ((IConfigurationRoot)app.Configuration).Reload();

        var rotated = await ReportApp.PostAsync(app, ReportBodies.Delivered, "Report rotated");
        var replaced = await ReportApp.PostAsync(app, ReportBodies.Delivered, "Report s3cret");

        rotated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        replaced.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{"to_address": "sms:+46701740605"}""")]
    public async Task A_body_that_is_not_a_report_is_answered_bad_request(string body)
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(app, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Received(app).Should().BeEmpty();
    }

    [Fact]
    public async Task A_body_the_server_refuses_is_answered_with_the_servers_status()
    {
        // TestServer sets no limit on a body, so this one runs on Kestrel.
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 100);
        builder.Configuration.AddInMemoryCollection(ReportApp.Settings);
        builder.Services.AddTellusTalkClient(builder.Configuration);
        builder.Services.AddTellusTalkReports<RecordingReportHandler>();
        builder.Services.AddSingleton<ReceivedReports>();
        await using var app = builder.Build();
        app.UseExceptionHandler(errors => errors.Run(_ => Task.CompletedTask));
        app.MapTellusTalkReports("/sms/reports");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/sms/reports", UriKind.Relative))
        {
            Content = new StringContent(ReportBodies.Delivered, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Report s3cret");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        Received(app).Should().BeEmpty();
    }

    [Fact]
    public async Task A_report_of_a_status_the_client_does_not_know_reaches_the_handler()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(
            app,
            ReportBodies.Delivered.Replace("txtDELIVERED", "txtQUEUED", StringComparison.Ordinal));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Received(app).Should().ContainSingle().Which.Status.Should().Be(DeliveryStatus.Unknown);
    }

    [Fact]
    public async Task A_handler_that_fails_leaves_the_answer_to_the_apps_error_handling()
    {
        await using var app = await ReportApp.StartAsync<FailingReportHandler>(map: web =>
        {
            web.UseExceptionHandler(errors => errors.Run(_ => Task.CompletedTask));
            web.MapTellusTalkReports("/sms/reports");
        });

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task An_app_whose_fallback_policy_requires_authenticated_users_takes_reports()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>(
            configure: builder =>
            {
                builder.Services
                    .AddAuthentication()
                    .AddCookie();

                builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
            },
            map: web =>
            {
                web.UseAuthentication();
                web.UseAuthorization();
                web.MapTellusTalkReports("/sms/reports");
            });

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_endpoint_can_be_mapped_in_a_route_group()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>(
            map: web => web.MapGroup("/integrations").MapTellusTalkReports("/sms/reports"));

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered, path: "/integrations/sms/reports");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Mapping_the_endpoint_without_the_reports_registration_is_refused()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(ReportApp.Settings);
        builder.Services.AddTellusTalkClient(builder.Configuration);
        await using var app = builder.Build();

        var map = () => app.MapTellusTalkReports("/sms/reports");

        map.Should().Throw<InvalidOperationException>().WithMessage("*AddTellusTalkReports*");
    }

    [Fact]
    public Task The_reports_registration_without_report_settings_fails_when_the_app_starts()
    {
        var start = () => ReportApp.StartAsync<RecordingReportHandler>(configure: builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(ReportApp.Credentials);
        });

        return start.Should().ThrowAsync<OptionsValidationException>().WithMessage("*Reports*");
    }

    private static ConcurrentQueue<DeliveryReport> Received(WebApplication app) =>
        app.Services.GetRequiredService<ReceivedReports>().Reports;
}
