using System.Text.Json;
using System.Text.Json.Nodes;

namespace Stabilt.TellusTalk.Client.Tests;

public sealed class DeliveryReportTests
{
    [Fact]
    public void A_delivered_report_is_read_with_its_fields()
    {
        var report = JsonSerializer.Deserialize<DeliveryReport>(ReportBodies.Delivered)!;

        report.Status.Should().Be(DeliveryStatus.Delivered);
        report.StatusText.Should().Be("txtDELIVERED");
        report.StatusCode.Should().Be(22);
        report.FailedReason.Should().BeNull();
        report.FailedDetails.Should().BeNull();
        report.To.Should().Be("+46701740605");
        report.JobUuid.Should().Be("5d656a5cdafd11f1ba39a2551b38e481");
        report.LogUuid.Should().Be("5d656a5cdafd11f1ba39a2551b38e482");
        report.Echo!.Value.GetProperty("appointment").GetInt32().Should().Be(4711);
        report.Parts.Should().Be(1);
        report.ReportTime.Should().Be(new DateTimeOffset(2026, 11, 7, 22, 0, 1, TimeSpan.Zero));
    }

    [Fact]
    public void A_failed_report_is_read_with_its_reason()
    {
        var report = JsonSerializer.Deserialize<DeliveryReport>(ReportBodies.Failed)!;

        report.Status.Should().Be(DeliveryStatus.Failed);
        report.StatusText.Should().Be("txtDELIVERY_FAILED");
        report.StatusCode.Should().Be(21);
        report.FailedReason.Should().Be("txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None");
    }

    [Fact]
    public void A_held_back_report_is_read_as_blocked()
    {
        var report = JsonSerializer.Deserialize<DeliveryReport>(ReportBodies.Blocked)!;

        report.Status.Should().Be(DeliveryStatus.Blocked);
        report.StatusCode.Should().Be(1);
        report.ReportTime.Should().BeNull();
        report.FailedDetails.Should().Be("Duplicate of message 5d656a5cdafd11f1ba39a2551b38e481 sent within 5 minutes.");
    }

    [Fact]
    public void A_status_the_client_does_not_know_is_read_as_unknown_with_its_text()
    {
        var report = ReadDelivered(body =>
        {
            body["log_status"] = "txtQUEUED";
            body["log_status_code"] = 5;
        });

        report.Status.Should().Be(DeliveryStatus.Unknown);
        report.StatusText.Should().Be("txtQUEUED");
        report.StatusCode.Should().Be(5);
    }

    [Fact]
    public void An_address_that_is_not_an_sms_number_is_kept_whole()
    {
        var report = ReadDelivered(body => body["to_address"] = "email:reports@app.invalid");

        report.To.Should().Be("email:reports@app.invalid");
    }

    [Fact]
    public void Optional_fields_of_another_type_are_read_as_missing()
    {
        var report = ReadDelivered(body =>
        {
            body["report_time"] = "yesterday";
            body["log_status_code"] = "22";
            body["quantity"] = "one";
        });

        report.Status.Should().Be(DeliveryStatus.Delivered);
        report.ReportTime.Should().BeNull();
        report.StatusCode.Should().BeNull();
        report.Parts.Should().BeNull();
    }

    [Fact]
    public void A_report_time_without_an_offset_is_read_as_utc()
    {
        var report = ReadDelivered(body => body["report_time"] = "2026-11-07T22:00:01");

        report.ReportTime.Should().BeExactly(new DateTimeOffset(2026, 11, 7, 22, 0, 1, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("""{"to_address": "sms:+46701740605"}""")]
    [InlineData("""{"log_status": 22}""")]
    [InlineData("[]")]
    [InlineData("\"txtDELIVERED\"")]
    public void A_body_without_a_status_text_is_not_a_report(string body)
    {
        var read = () => JsonSerializer.Deserialize<DeliveryReport>(body);

        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void A_report_written_as_json_is_read_back_as_it_was()
    {
        var report = JsonSerializer.Deserialize<DeliveryReport>(ReportBodies.Failed)!;

        var readBack = JsonSerializer.Deserialize<DeliveryReport>(JsonSerializer.Serialize(report))!;

        readBack.Should().BeEquivalentTo(report, options => options.ComparingByMembers<DeliveryReport>().Excluding(r => r.Echo));
        JsonElement.DeepEquals(readBack.Echo!.Value, report.Echo!.Value).Should().BeTrue();
    }

    private static DeliveryReport ReadDelivered(Action<JsonObject> change)
    {
        var body = JsonNode.Parse(ReportBodies.Delivered)!.AsObject();
        change(body);

        return JsonSerializer.Deserialize<DeliveryReport>(body.ToJsonString())!;
    }
}
