namespace ServiceControl.Infrastructure.WebApi
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class SerializerOptions
    {
        public static readonly JsonSerializerOptions Default = new JsonSerializerOptions().CustomizeDefaults();

        // TODO verify DateTimeStyles = DateTimeStyles.RoundtripKind
        // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft?pivots=dotnet-7-0#specify-date-format
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