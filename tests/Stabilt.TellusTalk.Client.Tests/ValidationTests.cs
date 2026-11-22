using System.Text.Json;

namespace Stabilt.TellusTalk.Client.Tests;

/// <summary>What TellusTalk's specification forbids is refused when the message is built, before anything is sent.</summary>
public sealed class ValidationTests
{
    [Theory]
    [InlineData("0701740605")]
    [InlineData("+46 70 174 06 05")]
    [InlineData("+4670174060a")]
    [InlineData("+")]
    [InlineData("sms:+46701740605")]
    public void A_recipient_that_is_not_a_plus_and_digits_is_refused(string number)
    {
        var build = () => new SmsMessage { To = [number], Text = "Hej" };

        build.Should().Throw<ArgumentException>().WithMessage($"To *{number}*");
    }

    [Fact]
    public void A_missing_recipient_is_refused()
    {
        var build = () => new SmsMessage { To = ["+46701740605", null!], Text = "Hej" };

        build.Should().Throw<ArgumentException>().WithMessage("To *");
    }

    [Fact]
    public void Recipients_changed_after_the_message_is_built_leave_the_message_as_it_was()
    {
        List<string> to = ["+46701740605"];
        var message = new SmsMessage { To = to, Text = "Hej" };

        to[0] = "0701740605";

        message.To.Should().Equal("+46701740605");
    }

    [Fact]
    public void A_missing_report_address_is_refused()
    {
        var build = () => new SmsMessage { To = ["+46701740605"], Text = "Hej", ReportTo = [null!] };

        build.Should().Throw<ArgumentException>().WithMessage("ReportTo *");
    }

    [Fact]
    public void A_missing_reply_address_is_refused()
    {
        var sender = () => Sender.ReplyNumber(replyTo: [null!]);

        sender.Should().Throw<ArgumentException>().WithParameterName("replyTo");
    }

    [Fact]
    public void Reply_addresses_changed_after_the_sender_is_built_leave_the_sender_as_it_was()
    {
        List<ReportAddress> replyTo = [ReportAddress.Sms("+46701740699")];
        var sender = Sender.ReplyNumber(replyTo);

        replyTo.Add(null!);

        sender.ReplyTo.Should().ContainSingle();
    }

    [Fact]
    public void Report_addresses_changed_after_the_message_is_built_leave_the_message_as_it_was()
    {
        List<ReportAddress> reportTo = [ReportAddress.Sms("+46701740699")];
        var message = new SmsMessage { To = ["+46701740605"], Text = "Hej", ReportTo = reportTo };

        reportTo.Add(null!);

        message.ReportTo.Should().ContainSingle();
    }

    [Fact]
    public void A_report_header_without_a_value_is_refused()
    {
        var address = () => ReportAddress.Url(
            new Uri("https://app.invalid/sms/reports"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = null! });

        address.Should().Throw<ArgumentException>().WithParameterName("headers");
    }

    [Fact]
    public void A_validity_time_beyond_what_fits_in_whole_seconds_is_refused()
    {
        var build = () => new SmsMessage { To = ["+46701740605"], Text = "Hej", ValidityTime = TimeSpan.FromSeconds(int.MaxValue + 1d) };

        build.Should().Throw<ArgumentOutOfRangeException>().WithMessage("ValidityTime *");
    }

    [Fact]
    public void A_message_without_recipients_is_refused()
    {
        var build = () => new SmsMessage { To = [], Text = "Hej" };

        build.Should().Throw<ArgumentException>().WithMessage("To *");
    }

    [Fact]
    public void A_sender_name_over_eleven_characters_is_refused()
    {
        var name = () => Sender.Name("Vårdcentralen");

        name.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void A_sender_name_of_eleven_characters_is_taken()
    {
        var name = () => Sender.Name("Vårdcentral");

        name.Should().NotThrow();
    }

    [Fact]
    public void An_empty_sender_name_is_refused()
    {
        var name = () => Sender.Name(" ");

        name.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void A_sender_subject_over_eleven_characters_is_refused()
    {
        var subject = () => Sender.Subject("Vårdcentralen");

        subject.Should().Throw<ArgumentException>().WithParameterName("subject");
    }

    [Fact]
    public void A_sender_subject_of_eleven_characters_is_taken()
    {
        var subject = () => Sender.Subject("Vårdcentral");

        subject.Should().NotThrow();
    }

    [Fact]
    public void An_empty_sender_subject_is_refused()
    {
        var subject = () => Sender.Subject(" ");

        subject.Should().Throw<ArgumentException>().WithParameterName("subject");
    }

    [Theory]
    [InlineData("0701740699")]
    [InlineData("+46 70 174 06 99")]
    public void A_sender_number_that_is_not_a_plus_and_digits_is_refused(string candidate)
    {
        var sender = () => Sender.Number(candidate);

        sender.Should().Throw<ArgumentException>().WithParameterName("number");
    }

    [Theory]
    [InlineData("ftp://app.invalid/reports")]
    [InlineData("file:///reports")]
    public void A_report_url_that_is_not_http_or_https_is_refused(string candidate)
    {
        var address = () => ReportAddress.Url(new Uri(candidate));

        address.Should().Throw<ArgumentException>().WithParameterName("url");
    }

    [Fact]
    public void A_relative_report_url_is_refused()
    {
        var address = () => ReportAddress.Url(new Uri("/reports", UriKind.Relative));

        address.Should().Throw<ArgumentException>().WithParameterName("url");
    }

    [Theory]
    [InlineData("reports")]
    [InlineData("reports@")]
    [InlineData("Reports <reports@app.invalid>")]
    public void A_report_email_that_is_not_an_address_is_refused(string email)
    {
        var address = () => ReportAddress.Email(email);

        address.Should().Throw<ArgumentException>().WithParameterName("address");
    }

    [Fact]
    public void A_report_number_that_is_not_a_plus_and_digits_is_refused()
    {
        var address = () => ReportAddress.Sms("0701740699");

        address.Should().Throw<ArgumentException>().WithParameterName("number");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Max_parts_below_one_is_refused(int maxParts)
    {
        var build = () => new SmsMessage { To = ["+46701740605"], Text = "Hej", MaxParts = maxParts };

        build.Should().Throw<ArgumentOutOfRangeException>().WithMessage("MaxParts *");
    }

    [Fact]
    public void A_validity_time_under_a_second_is_refused()
    {
        var build = () => new SmsMessage { To = ["+46701740605"], Text = "Hej", ValidityTime = TimeSpan.FromMilliseconds(999) };

        build.Should().Throw<ArgumentOutOfRangeException>().WithMessage("ValidityTime *");
    }

    [Fact]
    public void An_echo_that_holds_no_json_is_refused()
    {
        var build = () => new SmsMessage { To = ["+46701740605"], Text = "Hej", Echo = default(JsonElement) };

        build.Should().Throw<ArgumentException>().WithMessage("Echo *");
    }

    [Theory]
    [InlineData("ward-7")]
    [InlineData("ward 7")]
    [InlineData("avdelning7ä")]
    [InlineData("")]
    public void A_billing_id_that_is_not_letters_and_digits_is_refused(string billingId)
    {
        var build = () => new SmsMessage { To = ["+46701740605"], Text = "Hej", BillingId = billingId };

        build.Should().Throw<ArgumentException>().WithMessage("BillingId *");
    }
}
