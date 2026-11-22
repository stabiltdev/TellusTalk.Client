using System.Text.Json;
using Stabilt.TellusTalk.Emulator.Client;

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

        message.Warnings.Should().BeEmpty();
        message.Text.Should().Be("Din tid är bekräftad.");
        message.Sender!.Shown.Should().Be("Clinic");
        message.ReportTo[0].Headers["Authorization"].Should().Be("Bearer a+b/c=");
    }

    [Fact]
    public async Task A_subject_sender_reaches_the_emulator_without_warnings()
    {
        await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740616"], Text = "Din tid är bekräftad.", Sender = Sender.Subject("Clinic") },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740616");

        message.Warnings.Should().BeEmpty();
        message.Sender!.Shown.Should().Be("Clinic");
        message.Sender.Source.Should().Be("subject");
    }

    [Fact]
    public async Task A_number_sender_reaches_the_emulator_without_warnings()
    {
        await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740611"], Text = "Svara på det här numret.", Sender = Sender.Number("+46701740698") },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740611");

        message.Warnings.Should().BeEmpty();
        message.Sender!.Shown.Should().Be("+46701740698");
    }

    [Fact]
    public async Task A_reply_number_sender_reaches_the_emulator_without_warnings()
    {
        await emulator.Client.SendAsync(
            new SmsMessage
            {
                To = ["+46701740612"],
                Text = "Svara JA för att bekräfta.",
                Sender = Sender.ReplyNumber(
                    replyTo: [ReportAddress.Url(new Uri("https://app.invalid/sms/replies"))],
                    samePerRecipient: true),
            },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740612");

        message.Warnings.Should().BeEmpty();
        message.ReplyTo[0].Target.Should().Be("https://app.invalid/sms/replies");
    }

    [Fact]
    public async Task A_scheduled_message_waits_in_the_emulator_until_its_time()
    {
        var due = new DateTimeOffset(2099, 1, 1, 8, 30, 0, TimeSpan.FromHours(1));

        await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740613"], Text = "Påminnelse.", ScheduledSendTime = due },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740613");

        message.Warnings.Should().BeEmpty();
        message.State.Should().Be(MessageState.Scheduled);
        message.DueAt.Should().Be(due);
    }

    [Fact]
    public async Task The_emulators_answer_to_several_recipients_is_read_into_the_send_result()
    {
        var result = await emulator.Client.SendAsync(
            new SmsMessage { To = ["+46701740614", "+46701740615"], Text = "Till två." },
            TestContext.Current.CancellationToken);

        var message = await SingleMessageAsync("+46701740614");

        result.JobSize.Should().Be(2);
        result.JobId.Should().Be(message.JobId);
        result.LogUuid.Should().BeNull();
    }

    private async Task<CaughtMessage> SingleMessageAsync(string to)
    {
        var messages = await emulator.Api.GetMessagesAsync(new MessageQuery { To = to }, TestContext.Current.CancellationToken);

        return messages.Should().ContainSingle().Subject;
    }
}
