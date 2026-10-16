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

        // Without whitespace between the entries, the SQL Server word breaker keeps JSON quotes on the words near an email address or a URL. A search for those words then finds nothing.
        WriteIndented = true,
        IndentSize = 0
    });
}

// Source generated serialization, which keeps the reflection-based serializer off the ingestion hot path.
[JsonSerializable(typeof(Dictionary<string, string>))]
partial class HeadersJsonContext : JsonSerializerContext;
