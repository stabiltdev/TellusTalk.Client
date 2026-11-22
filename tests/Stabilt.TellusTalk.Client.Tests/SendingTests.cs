using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Stabilt.TellusTalk.Client.Tests.Support;

namespace Stabilt.TellusTalk.Client.Tests;

public sealed class SendingTests
{
    private static readonly SmsMessage Minimal = new()
    {
        To = ["+46701740605"],
        Text = "Din tid är bekräftad.",
    };

    [Fact]
    public async Task A_send_posts_json_to_send_v1_under_the_base_url()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri("https://k8api.tellustalk.com/send/v1"));
        handler.Request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task A_send_carries_its_length_rather_than_being_chunked()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal);

        handler.Request!.Content!.Headers.ContentLength.Should().Be(Encoding.UTF8.GetByteCount(handler.Body!));
        handler.Request.Headers.TransferEncodingChunked.Should().NotBe(true);
    }

    [Fact]
    public async Task A_send_carries_the_credentials_as_basic_authentication()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal);

        var authorization = handler.Request!.Headers.Authorization!;
        authorization.Scheme.Should().Be("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Parameter!)).Should().Be("tester:secret");
    }

    [Fact]
    public async Task A_message_of_recipients_and_text_sends_those_fields_alone_and_unescaped()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal);

        handler.Body.Should().Be("""{"to":["sms:+46701740605"],"text":"Din tid är bekräftad."}""");
    }

    [Fact]
    public async Task Every_recipient_is_sent_as_an_sms_address()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { To = ["+46701740605", "+46701740606"] });

        Field(handler, "to").Should().Be("""["sms:+46701740605","sms:+46701740606"]""");
    }

    [Fact]
    public async Task A_named_sender_is_sent_as_text()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { Sender = Sender.Name("Clinic") });

        handler.Body.Should().Contain("""
            "sms_originator_source":"text","sms_originator_text":"Clinic"
            """);
    }

    [Fact]
    public async Task A_subject_sender_is_sent_as_subject()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { Sender = Sender.Subject("Clinic") });

        handler.Body.Should().Contain("""
            "sms_originator_source":"subject","subject":"Clinic"
            """);
    }

    [Fact]
    public async Task A_number_sender_is_sent_as_number()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { Sender = Sender.Number("+46701740699") });

        handler.Body.Should().Contain("""
            "sms_originator_source":"number","sms_originator_number":"+46701740699"
            """);
    }

    [Fact]
    public async Task A_reply_number_sender_is_sent_as_replysms()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { Sender = Sender.ReplyNumber() });

        handler.Body.Should().Contain("""
            "sms_originator_source":"replysms"
            """);
        handler.Body.Should().NotContain("sms_single_thread");
    }

    [Fact]
    public async Task A_reply_number_kept_per_recipient_is_sent_as_a_single_thread()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { Sender = Sender.ReplyNumber(samePerRecipient: true) });

        handler.Body.Should().Contain("""
            "sms_originator_source":"replysms","sms_single_thread":true
            """);
    }

    [Fact]
    public async Task A_scheduled_time_is_sent_with_its_offset()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { ScheduledSendTime = new DateTimeOffset(2026, 11, 9, 8, 30, 0, TimeSpan.FromHours(1)) });

        Field(handler, "scheduled_send_time").Should().Be("\"2026-11-09T08:30:00+01:00\"");
    }

    [Fact]
    public async Task Report_addresses_are_sent_in_tellustalks_forms()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with
        {
            ReportTo =
            [
                ReportAddress.Url(new Uri("https://app.invalid/sms/reports")),
                ReportAddress.Url(
                    new Uri("https://app.invalid/sms/reports?source=k8"),
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer a+b/c=" }),
                ReportAddress.Email("reports@app.invalid"),
                ReportAddress.Sms("+46701740699"),
            ],
        });

        Field(handler, "report_to").Should().Be(
            """["https://app.invalid/sms/reports","https:{\"headers\":{\"Authorization\":\"Bearer a+b/c=\"}}://app.invalid/sms/reports?source=k8","email:reports@app.invalid","sms:+46701740699"]""");
    }

    [Fact]
    public async Task Reply_addresses_are_sent_as_reply_to()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with
        {
            Sender = Sender.ReplyNumber(replyTo: [ReportAddress.Url(new Uri("https://app.invalid/sms/replies"))]),
        });

        Field(handler, "reply_to").Should().Be("""["https://app.invalid/sms/replies"]""");
    }

    [Fact]
    public async Task The_sms_settings_are_sent_under_tellustalks_names()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with
        {
            MaxParts = 3,
            Flash = true,
            ValidityTime = TimeSpan.FromHours(2),
            CharacterSet = SmsCharacterSet.Unicode,
            BillingId = "Ward7",
        });

        handler.Body.Should().Contain("""
            "sms_max_parts":3,"sms_flash":true,"sms_validity_time":7200,"sms_character_set":"unicode","billing_id":"Ward7"
            """);
    }

    [Theory]
    [InlineData(SmsCharacterSet.Optimize, "optimize")]
    [InlineData(SmsCharacterSet.Gsm, "gsm")]
    [InlineData(SmsCharacterSet.Unicode, "unicode")]
    public async Task Each_character_set_is_sent_by_tellustalks_name(SmsCharacterSet characterSet, string name)
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { CharacterSet = characterSet });

        Field(handler, "sms_character_set").Should().Be($"\"{name}\"");
    }

    [Fact]
    public async Task An_echo_is_sent_as_the_json_it_holds()
    {
        using var handler = new RecordingHandler();

        await SendAsync(handler, Minimal with { Echo = JsonSerializer.SerializeToElement(new { appointment = 4711 }) });

        Field(handler, "echo").Should().Be("""{"appointment":4711}""");
    }

    [Fact]
    public async Task An_echo_outlives_the_document_it_came_from()
    {
        using var handler = new RecordingHandler();
        var document = JsonDocument.Parse("""{"appointment":4711}""");
        var message = Minimal with { Echo = document.RootElement };
        document.Dispose();

        await SendAsync(handler, message);

        Field(handler, "echo").Should().Be("""{"appointment":4711}""");
    }

    [Fact]
    public async Task The_answer_is_read_into_the_send_result()
    {
        using var handler = new RecordingHandler();

        var result = await SendAsync(handler, Minimal);

        result.Should().Be(new SendResult(
            "5d656a5cdafd11f1ba39a2551b38e481",
            "5d656a5cdafd11f1ba39a2551b38e481",
            "5d656a5cdafd11f1ba39a2551b38e482",
            1));
    }

    [Fact]
    public async Task An_answer_to_several_recipients_is_read_without_a_log_id()
    {
        using var handler = new RecordingHandler
        {
            Answer = """{"job_id": "5d656a5cdafd11f1ba39a2551b38e481", "job_uuid": "5d656a5cdafd11f1ba39a2551b38e481", "job_size": 2}""",
        };

        var result = await SendAsync(handler, Minimal with { To = ["+46701740605", "+46701740606"] });

        result.LogUuid.Should().BeNull();
        result.JobSize.Should().Be(2);
    }

    [Fact]
    public async Task The_addresses_tellustalk_could_not_use_are_read_with_their_errors()
    {
        using var handler = new RecordingHandler
        {
            Answer = """
                {"job_id": "5d656a5cdafd11f1ba39a2551b38e481", "job_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
                 "invalid_report_to": [{"address": "reports", "error": "Unknown Address Type"}],
                 "invalid_reply_to": [{"address": "replies", "error": "Unknown Address Type"}],
                 "log_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
                 "invalid_recipients": [{"address": "sms:+4670174062X", "error": "Number Not Numeric"}],
                 "job_size": 1}
                """,
        };

        var result = await SendAsync(handler, Minimal);

        result.InvalidRecipients.Should().Equal(new InvalidAddress("sms:+4670174062X", "Number Not Numeric"));
        result.InvalidReportTo.Should().Equal(new InvalidAddress("reports", "Unknown Address Type"));
        result.InvalidReplyTo.Should().Equal(new InvalidAddress("replies", "Unknown Address Type"));
    }

    [Fact]
    public async Task An_answer_that_lists_no_addresses_is_read_as_empty_lists()
    {
        using var handler = new RecordingHandler();

        var result = await SendAsync(handler, Minimal);

        result.InvalidRecipients.Should().BeEmpty();
        result.InvalidReportTo.Should().BeEmpty();
        result.InvalidReplyTo.Should().BeEmpty();
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2025:Ensure tasks using IDisposable instances complete before the instances are disposed", Justification = "The assertion awaits the send before the handler is disposed.")]
    public async Task A_refusal_throws_with_tellustalks_status_and_text()
    {
        using var handler = new RecordingHandler
        {
            Status = HttpStatusCode.Unauthorized,
            ContentType = "text/html",
            Answer = "The server could not verify that you are authorized to access the URL requested.",
        };

        var send = () => SendAsync(handler, Minimal);

        var refusal = (await send.Should().ThrowAsync<TellusTalkException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        refusal.ResponseText.Should().Be("The server could not verify that you are authorized to access the URL requested.");
        refusal.Message.Should().Contain("401").And.Contain("could not verify");
        refusal.Accepted.Should().BeFalse();
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2025:Ensure tasks using IDisposable instances complete before the instances are disposed", Justification = "The assertion awaits the send before the handler is disposed.")]
    public async Task A_success_that_is_not_a_send_answer_throws_saying_tellustalk_took_the_message()
    {
        using var handler = new RecordingHandler { ContentType = "text/html", Answer = "<html>maintenance</html>" };

        var send = () => SendAsync(handler, Minimal);

        var refusal = (await send.Should().ThrowAsync<TellusTalkException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.OK);
        refusal.ResponseText.Should().Be("<html>maintenance</html>");
        refusal.Accepted.Should().BeTrue();
    }

    private static async Task<SendResult> SendAsync(RecordingHandler handler, SmsMessage message)
    {
        await using var services = Clients.Services(handler);

        return await services
            .GetRequiredService<TellusTalkClient>()
            .SendAsync(message, TestContext.Current.CancellationToken);
    }

    private static string Field(RecordingHandler handler, string name)
    {
        using var body = JsonDocument.Parse(handler.Body!);

        return body.RootElement.GetProperty(name).GetRawText();
    }
}
