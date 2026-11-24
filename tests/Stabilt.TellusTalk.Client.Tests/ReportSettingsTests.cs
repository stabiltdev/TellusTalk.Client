using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client.Tests.Support;

namespace Stabilt.TellusTalk.Client.Tests;

public sealed class ReportSettingsTests
{
    private static readonly SmsMessage Message = new() { To = ["+46701740605"], Text = "Hej" };

    [Fact]
    public async Task Every_message_carries_the_report_address_with_the_secret_in_authorization()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.WithReports());
        var client = services.GetRequiredService<TellusTalkClient>();

        await client.SendAsync(Message, TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Be(
            """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports"]""");
    }

    [Fact]
    public async Task The_report_address_comes_before_the_messages_own_report_addresses()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.WithReports());
        var client = services.GetRequiredService<TellusTalkClient>();

        await client.SendAsync(
            Message with { ReportTo = [ReportAddress.Email("reports@app.invalid")] },
            TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Be(
            """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports","email:reports@app.invalid"]""");
    }

    [Fact]
    public async Task The_report_address_keeps_the_query_of_the_report_url()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.WithReports(url: "https://app.invalid/sms/reports?source=k8"));
        var client = services.GetRequiredService<TellusTalkClient>();

        await client.SendAsync(Message, TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Be(
            """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports?source=k8"]""");
    }

    [Fact]
    public async Task Without_report_settings_only_the_messages_own_report_addresses_are_sent()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler);
        var client = services.GetRequiredService<TellusTalkClient>();

        await client.SendAsync(
            Message with { ReportTo = [ReportAddress.Email("reports@app.invalid")] },
            TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Be("""["email:reports@app.invalid"]""");
    }

    [Fact]
    public async Task The_report_address_follows_a_secret_rotated_in_configuration()
    {
        var configuration = Clients.Configuration(Clients.WithReports());

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, services => services.AddTellusTalkClient(configuration));
        var client = services.GetRequiredService<TellusTalkClient>();
        await client.SendAsync(Message, TestContext.Current.CancellationToken);

        configuration["TellusTalk:Reports:Secret"] = "rotated";
        configuration.Reload();
        await client.SendAsync(Message, TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Contain("Report rotated");
    }

    [Theory]
    [InlineData(null, "s3cret")]
    [InlineData("/sms/reports", "s3cret")]
    [InlineData("ftp://app.invalid/sms/reports", "s3cret")]
    [InlineData("https://app.invalid/sms/reports", null)]
    [InlineData("https://app.invalid/sms/reports", " ")]
    public async Task Report_settings_without_an_http_url_or_a_secret_are_refused(string? url, string? secret)
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.WithReports(url, secret));

        var create = services.GetRequiredService<TellusTalkClient>;

        create.Should().Throw<OptionsValidationException>().WithMessage("*Reports*");
    }

    [Theory]
    [InlineData("TellusTalk:Reports:Secret", "s3cret ")]
    [InlineData("TellusTalk:Reports:Secret", " s3cret")]
    [InlineData("TellusTalk:Reports:Secret", "s3c ret")]
    [InlineData("TellusTalk:Reports:Secret", "s3cret\n")]
    [InlineData("TellusTalk:Reports:Secret", "s3crét")]
    [InlineData("TellusTalk:Reports:PreviousSecrets:0", "0ld ")]
    public async Task A_secret_that_is_not_visible_ascii_is_refused(string key, string secret)
    {
        var settings = Clients.WithReports();
        settings[key] = secret;

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, settings);

        var create = services.GetRequiredService<TellusTalkClient>;

        create.Should().Throw<OptionsValidationException>().WithMessage("*visible ASCII*");
    }

    [Fact]
    public async Task An_empty_previous_secret_is_refused()
    {
        var settings = Clients.WithReports();
        settings["TellusTalk:Reports:PreviousSecrets:0"] = "";

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, settings);

        var create = services.GetRequiredService<TellusTalkClient>;

        create.Should().Throw<OptionsValidationException>().WithMessage("*PreviousSecrets*");
    }

    private static string Field(RecordingHandler handler, string name)
    {
        using var body = JsonDocument.Parse(handler.Body!);

        return body.RootElement.GetProperty(name).GetRawText();
    }
}
