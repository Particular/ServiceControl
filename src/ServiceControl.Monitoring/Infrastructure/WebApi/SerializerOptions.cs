namespace ServiceControl.Monitoring.Infrastructure;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class SerializerOptions
{
    public static readonly JsonSerializerOptions Default = new JsonSerializerOptions().CustomizeDefaults();

    public static JsonSerializerOptions CustomizeDefaults(this JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.WriteIndented = false;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}