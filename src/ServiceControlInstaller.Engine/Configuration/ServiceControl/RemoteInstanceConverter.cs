namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class RemoteInstanceConverter
    {
        public static List<RemoteInstanceSetting> FromJson(string json) => JsonSerializer.Deserialize(json, RemoteInstanceContext.Default.ListRemoteInstanceSetting);

        public static string ToJson(List<RemoteInstanceSetting> settings) => JsonSerializer.Serialize(settings, RemoteInstanceContext.Default.ListRemoteInstanceSetting);
    }

    [JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(List<RemoteInstanceSetting>))]
    public partial class RemoteInstanceContext : JsonSerializerContext;

    public class RemoteInstanceSetting
    {
        [JsonPropertyName("api_uri")]
        public string ApiUri { get; set; }

        [JsonPropertyName("queue_address")]
        public string QueueAddress { get; set; }
    }
}
