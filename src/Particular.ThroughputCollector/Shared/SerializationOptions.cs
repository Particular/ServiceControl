namespace Particular.ThroughputCollector.Shared;

using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;

public static class SerializationOptions
{
    public static readonly JsonSerializerOptions IndentedWithNoEscaping = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly JsonSerializerOptions NotIndentedWithNoEscaping = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
