using System.Net.Http.Json;
using System.Text.Json;

namespace Stabilt.TellusTalk.Client.Tests.Emulator;

/// <summary>Each test sends to numbers of its own, so the tests share one emulator without seeing each other's messages.</summary>
public sealed class EmulatorTests(EmulatorContainer emulator) : IClassFixture<EmulatorContainer>
{
    [Fact]
    public async Task A_message_with_every_setting_reaches_the_emulator_without_warnings()
    {
        await emulator.Client.SendAsync(
            new SmsMessage
            {
                To = ["+46701740610"],
                Text = "Din tid är bekräftad.",
                Sender = Sender.Name("Clinic"),
                ReportTo =
                [
                    ReportAddress.Url(
                        new Uri("https://app.invalid/sms/reports"),
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer a+b/c=" }),
                    ReportAddress.Email("reports@app.invalid"),
                    ReportAddress.Sms("+46701740699"),
                ],
                MaxParts = 2,
                Flash = true,
                ValidityTime = TimeSpan.FromHours(2),
                CharacterSet = SmsCharacterSet.Gsm,
                BillingId = "Ward7",
                Echo = JsonSerializer.SerializeToElement(new { appointment = 4711 }),
            },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740610");

        message.GetProperty("warnings").EnumerateArray().Should().BeEmpty();
        message.GetProperty("text").GetString().Should().Be("Din tid är bekräftad.");
        message.GetProperty("sender").GetProperty("shown").GetString().Should().Be("Clinic");
        message.GetProperty("reportTo")[0].GetProperty("headers").GetProperty("Authorization").GetString().Should().Be("Bearer a+b/c=");
    }

    [Fact]
    public async Task A_number_sender_reaches_the_emulator_without_warnings()
    {
        await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740611"], Text = "Svara på det här numret.", Sender = Sender.Number("+46701740698") },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740611");

        message.GetProperty("warnings").EnumerateArray().Should().BeEmpty();
        message.GetProperty("sender").GetProperty("shown").GetString().Should().Be("+46701740698");
    }

    [Fact]
    public async Task A_reply_number_sender_reaches_the_emulator_without_warnings()
    {
        await emulator.Client.SendAsync(
            new SmsMessage
            {
                To = ["+46701740612"],
                Text = "Svara JA för att bekräfta.",
                Sender = Sender.ReplyNumber(samePerRecipient: true),
                ReplyTo = [ReportAddress.Url(new Uri("https://app.invalid/sms/replies"))],
            },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740612");

        message.GetProperty("warnings").EnumerateArray().Should().BeEmpty();
        message.GetProperty("replyTo")[0].GetProperty("target").GetString().Should().Be("https://app.invalid/sms/replies");
    }

    [Fact]
    public async Task A_scheduled_message_waits_in_the_emulator_until_its_time()
    {
        var due = new DateTimeOffset(2099, 1, 1, 8, 30, 0, TimeSpan.FromHours(1));

        await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740613"], Text = "Påminnelse.", ScheduledSendTime = due },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740613");

        message.GetProperty("warnings").EnumerateArray().Should().BeEmpty();
        message.GetProperty("state").GetString().Should().Be("scheduled");
        message.GetProperty("dueAt").GetDateTimeOffset().Should().Be(due);
    }

    [Fact]
    public async Task The_emulators_answer_is_read_into_the_send_result()
    {
        var result = await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740614", "+46701740615"], Text = "Till två." },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740614");

        result.JobSize.Should().Be(2);
        result.JobId.Should().Be(message.GetProperty("jobId").GetString());
    }

    private async Task<JsonElement> SingleMessageAsync(string to)
    {
        var list = await emulator.Api.GetFromJsonAsync<JsonElement>(
            $"api/v1/messages?to={Uri.EscapeDataString(to)}",
            TestContext.Current.CancellationToken);

        list.GetProperty("total").GetInt32().Should().Be(1);

        return list.GetProperty("messages")[0];
    }
}
