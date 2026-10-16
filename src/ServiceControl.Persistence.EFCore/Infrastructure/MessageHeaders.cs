namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

// The headers of a failed message are stored verbatim as the HeadersJson column.
static class MessageHeaders
{
    public static string Write(Dictionary<string, string> headers) =>
        JsonSerializer.Serialize(headers, context.DictionaryStringString);

    public static Dictionary<string, string> Read(string headersJson) =>
        JsonSerializer.Deserialize(headersJson, context.DictionaryStringString) ?? [];

    static readonly HeadersJsonContext context = new(new JsonSerializerOptions
    {
        // The relaxed encoder is only unsafe for JSON embedded in HTML, and this JSON never reaches HTML. Full text search needs apostrophes, plus signs and non-ASCII letters written as they are, or it cannot find the words next to them.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        // In compact JSON, SQL Server's word breaker keeps JSON quotes and colons on the words before an email address or a URL, and a search cannot find those words. An indent of 0 adds only the whitespace that stops this: a line break after each entry and a space after each colon.
        WriteIndented = true,
        IndentSize = 0
    });
}

// Source generated serialization, which keeps the reflection-based serializer off the ingestion hot path.
[JsonSerializable(typeof(Dictionary<string, string>))]
partial class HeadersJsonContext : JsonSerializerContext;
