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
