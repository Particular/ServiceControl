namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

// The headers of a failed message are stored verbatim as the HeadersJson column.
static class MessageHeaders
{
    public static string Write(Dictionary<string, string> headers) =>
        JsonSerializer.Serialize(headers, context.DictionaryStringString);

    public static Dictionary<string, string> Read(string headersJson) =>
        JsonSerializer.Deserialize(headersJson, context.DictionaryStringString) ?? [];

    // The default encoder escapes every non-ASCII character, and full text search then indexes the escape sequence instead of the word. This one writes letters outside ASCII as they are, while HTML sensitive characters and emoji stay escaped.
    static readonly HeadersJsonContext context = new(new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
}

// Source generated serialization, which keeps the reflection-based serializer off the ingestion hot path.
[JsonSerializable(typeof(Dictionary<string, string>))]
partial class HeadersJsonContext : JsonSerializerContext;
