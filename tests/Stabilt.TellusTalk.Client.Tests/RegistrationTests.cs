using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client.Tests.Support;

namespace Stabilt.TellusTalk.Client.Tests;

public sealed class RegistrationTests
{
    private static readonly SmsMessage Message = new() { To = ["+46701740605"], Text = "Hej" };

    private static readonly IReadOnlyDictionary<string, string?> SmsSection = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Sms:BaseUrl"] = "http://localhost:8080",
        ["Sms:UserName"] = "tester",
        ["Sms:Password"] = "secret",
    };

    [Fact]
    public async Task The_base_url_comes_from_the_TellusTalk_section()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.With("TellusTalk:BaseUrl", "http://localhost:8080"));

        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.RequestUri.Should().Be(new Uri("http://localhost:8080/send/v1"));
    }

    [Fact]
    public async Task A_base_url_with_a_path_keeps_its_path()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.With("TellusTalk:BaseUrl", "http://localhost:8080/k8"));

        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.RequestUri.Should().Be(new Uri("http://localhost:8080/k8/send/v1"));
    }

    [Fact]
    public async Task A_section_of_the_apps_own_name_is_read_under_the_configuration()
    {
        var configuration = Clients.Configuration(SmsSection);

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, services => services.AddTellusTalkClient(configuration, "Sms"));

        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.RequestUri.Should().Be(new Uri("http://localhost:8080/send/v1"));
    }

    [Fact]
    public async Task A_section_passed_alone_is_read_as_it_is()
    {
        var configuration = Clients.Configuration(SmsSection);

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, services => services.AddTellusTalkClient(configuration.GetSection("Sms")));

        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.RequestUri.Should().Be(new Uri("http://localhost:8080/send/v1"));
    }

    [Fact]
    public async Task Code_alone_configures_the_client()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(
            handler,
            services => services.AddTellusTalkClient(options =>
            {
                options.UserName = "coded";
                options.Password = "secret";
            }));

        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.Headers.Authorization!.Parameter.Should().Be(Convert.ToBase64String("coded:secret"u8));
    }

    [Fact]
    public async Task A_password_changed_in_configuration_reaches_the_next_client()
    {
        var configuration = Clients.Configuration();

        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, services => services.AddTellusTalkClient(configuration));
        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        configuration["TellusTalk:Password"] = "rotated";
        configuration.Reload();
        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.Headers.Authorization!.Parameter.Should().Be(Convert.ToBase64String("tester:rotated"u8));
    }

    [Theory]
    [InlineData("TellusTalk:UserName")]
    [InlineData("TellusTalk:Password")]
    public async Task Missing_credentials_are_refused_when_the_client_is_created(string key)
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.With(key, null));

        var create = services.GetRequiredService<TellusTalkClient>;

        create.Should().Throw<OptionsValidationException>().WithMessage($"*{key.Split(':')[1]}*");
    }

    [Theory]
    [InlineData("localhost:8080")]
    [InlineData("/k8")]
    [InlineData("ftp://k8api.tellustalk.com/")]
    public async Task A_base_url_that_is_not_an_absolute_http_url_is_refused(string baseUrl)
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler, Clients.With("TellusTalk:BaseUrl", baseUrl));

        var create = services.GetRequiredService<TellusTalkClient>;

        create.Should().Throw<OptionsValidationException>().WithMessage("*BaseUrl*");
    }

    [Fact]
    public async Task The_client_names_itself_in_the_user_agent()
    {
        using var handler = new RecordingHandler();
        await using var services = Clients.Services(handler);

        await services.GetRequiredService<TellusTalkClient>().SendAsync(Message, TestContext.Current.CancellationToken);

        handler.Request!.Headers.UserAgent.ToString().Should().Be("Stabilt.TellusTalk.Client/0.1.0");
    }
}
