namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class RemoteInstanceConverter
    {
        public static RemoteInstanceSetting[] FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RemoteInstanceSetting[]>(json, Settings);
        }

        public static string ToJson(RemoteInstanceSetting[] settings)
        {
            return JsonConvert.SerializeObject(settings, Settings);
        }

        static JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public class RemoteInstanceSetting
    {
        public string ApiUri { get; set; }
        public string QueueAddress { get; set; }
    }
}
