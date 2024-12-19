namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class RemoteInstanceConverter
    {
        public static List<RemoteInstanceSetting> FromJson(string json) => JsonSerializer.Deserialize<List<RemoteInstanceSetting>>(json, Options);

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