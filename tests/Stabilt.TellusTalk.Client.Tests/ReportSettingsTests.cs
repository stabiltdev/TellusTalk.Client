using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client.Tests.Support;

namespace Stabilt.TellusTalk.Client.Tests;

public sealed class ReportSettingsTests
{
    private static readonly SmsMessage Message = new() { To = ["+46701740605"], Text = "Hej" };

    [Fact]
    public async Task The_report_address_carries_the_secret_in_authorization()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.WithReports());
        var client = services.GetRequiredService<TellusTalkClient>();

        await client.SendAsync(Message with { ReportTo = [client.ReportAddress] }, TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Be(
            """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports"]""");
    }

    [Fact]
    public async Task The_report_address_follows_a_secret_rotated_in_configuration()
    {
        var configuration = Clients.Configuration(Clients.WithReports());

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, services => services.AddTellusTalkClient(configuration));
        var client = services.GetRequiredService<TellusTalkClient>();
        await client.SendAsync(Message with { ReportTo = [client.ReportAddress] }, TestContext.Current.CancellationToken);

        configuration["TellusTalk:Reports:Secret"] = "rotated";
        configuration.Reload();
        await client.SendAsync(Message with { ReportTo = [client.ReportAddress] }, TestContext.Current.CancellationToken);

        Field(handler, "report_to").Should().Contain("Report rotated");
    }

    [Fact]
    public async Task The_report_address_without_report_settings_is_refused()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler);
        var client = services.GetRequiredService<TellusTalkClient>();

        var address = () => client.ReportAddress;

        address.Should().Throw<InvalidOperationException>().WithMessage("*Reports*");
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
