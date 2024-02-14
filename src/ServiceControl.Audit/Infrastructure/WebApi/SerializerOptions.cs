namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class SerializerOptions
    {
        public static readonly JsonSerializerOptions Default = new JsonSerializerOptions().CustomizeDefaults();

        public static JsonSerializerOptions CustomizeDefaults(this JsonSerializerOptions options)
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.WriteIndented = false;
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}