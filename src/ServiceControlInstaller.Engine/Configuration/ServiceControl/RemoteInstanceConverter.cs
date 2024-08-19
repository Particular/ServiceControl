namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class RemoteInstanceConverter
    {
        public static List<RemoteInstanceSetting> FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<RemoteInstanceSetting>>(json, Options);
            }
            catch (JsonException)
            {
                // It is possible that the value is using single quotes, this was allowed in v4 and below, but with v5 we migrated to System.Text.Json which does not support single quotes in either property names nor values.
                // See https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft?pivots=dotnet-9-0#json-strings-property-names-and-string-values
                return JsonSerializer.Deserialize<List<RemoteInstanceSetting>>(json.Replace('\'', '"'), Options);
            }
        }

        public static string ToJson(IList<RemoteInstanceSetting> settings) => JsonSerializer.Serialize(settings, Options);

        static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public class RemoteInstanceSetting
    {
        [JsonPropertyName("api_uri")]
        public string ApiUri { get; set; }

        [JsonPropertyName("queue_address")]
        public string QueueAddress { get; set; }
    }
}
