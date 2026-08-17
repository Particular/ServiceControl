namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Text.Json;
using System.Text.Json.Serialization;

// The headers of a failed message are stored verbatim as the HeadersJson column.
static class MessageHeaders
{
    public static string Write(Dictionary<string, string> headers) =>
        JsonSerializer.Serialize(headers, HeadersJsonContext.Default.DictionaryStringString);

    public static Dictionary<string, string> Read(string headersJson) =>
        JsonSerializer.Deserialize(headersJson, HeadersJsonContext.Default.DictionaryStringString) ?? [];
}

// Source generated serialization, which keeps the reflection-based serializer off the ingestion hot path.
[JsonSerializable(typeof(Dictionary<string, string>))]
partial class HeadersJsonContext : JsonSerializerContext;
