using System.Net;
using System.Text;

namespace Stabilt.TellusTalk.Client.Tests.Support;

/// <summary>Stands in for TellusTalk: keeps the request it was sent and gives the answer it was set up with.</summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    public const string SendAnswer =
        """{"job_id": "5d656a5cdafd11f1ba39a2551b38e481", "job_uuid": "5d656a5cdafd11f1ba39a2551b38e481", "log_uuid": "5d656a5cdafd11f1ba39a2551b38e482", "job_size": 1}""";

    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

    public string ContentType { get; init; } = "application/json";

    public string Answer { get; init; } = SendAnswer;

    public HttpRequestMessage? Request { get; private set; }

    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(Status)
        {
            Content = new StringContent(Answer, Encoding.UTF8, ContentType),
        };
    }
}
