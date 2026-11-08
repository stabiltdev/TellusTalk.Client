using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stabilt.TellusTalk.Client;

internal static class Json
{
    /// <summary>
    /// TellusTalk reads the body as JSON, never as HTML, so nothing needs escaping for a browser; unescaped, the body
    /// stays readable wherever it is shown as sent, and a report URL's headers keep their <c>+</c>.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
