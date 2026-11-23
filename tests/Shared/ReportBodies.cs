namespace Stabilt.TellusTalk.Client.Tests;

/// <summary>Delivery reports as K8 posts them, with every field it was seen sending.</summary>
internal static class ReportBodies
{
    public const string Delivered = """
        {
          "log_status": "txtDELIVERED",
          "log_status_code": 22,
          "failed_reason": "",
          "article_id": "SMS_Sent",
          "to_protocol": "SMS",
          "to_address": "sms:+46701740605",
          "to_name": null,
          "from_address": "user_id:tester",
          "from_name": "",
          "subject": null,
          "text": "Your appointment is confirmed.",
          "html": "Your appointment is confirmed.",
          "echo": {"appointment": 4711},
          "quantity": 1,
          "log_uuid": "5d656a5cdafd11f1ba39a2551b38e482",
          "conversation_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
          "job_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
          "report_time": "2026-11-07T22:00:01.000Z",
          "failed_details": null,
          "text_linebreaksbr": "Your appointment is confirmed.",
          "template_optional_line": "--------------"
        }
        """;

    public const string Failed = """
        {
          "log_status": "txtDELIVERY_FAILED",
          "log_status_code": 21,
          "failed_reason": "txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None",
          "article_id": "SMS_Sent",
          "to_protocol": "SMS",
          "to_address": "sms:+46701740605",
          "to_name": null,
          "from_address": "user_id:tester",
          "from_name": "",
          "subject": null,
          "text": "Your appointment is confirmed.",
          "html": "Your appointment is confirmed.",
          "echo": {"appointment": 4711},
          "quantity": 1,
          "log_uuid": "5d656a5cdafd11f1ba39a2551b38e482",
          "conversation_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
          "job_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
          "report_time": "2026-11-07T22:00:01.000Z",
          "failed_details": null,
          "text_linebreaksbr": "Your appointment is confirmed.",
          "template_optional_line": "--------------"
        }
        """;

    public const string Blocked = """
        {
          "log_status": "txtBLOCKED",
          "log_status_code": 1,
          "failed_reason": "txtFAILED_REASON: txtDUPLICATE_MESSAGE, Duplicate of message 5d656a5cdafd11f1ba39a2551b38e481 sent within 5 minutes.",
          "article_id": "SMS_Sent",
          "to_protocol": "SMS",
          "to_address": "sms:+46701740605",
          "to_name": null,
          "from_address": "user_id:tester",
          "from_name": "",
          "subject": null,
          "text": "Your appointment is confirmed.",
          "html": "Your appointment is confirmed.",
          "echo": {"appointment": 4711},
          "quantity": 1,
          "log_uuid": "5d656a5cdafd11f1ba39a2551b38e482",
          "conversation_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
          "job_uuid": "5d656a5cdafd11f1ba39a2551b38e481",
          "report_time": null,
          "failed_details": "Duplicate of message 5d656a5cdafd11f1ba39a2551b38e481 sent within 5 minutes.",
          "text_linebreaksbr": "Your appointment is confirmed.",
          "template_optional_line": "--------------"
        }
        """;
}
