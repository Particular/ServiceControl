namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class RemoteInstanceConverter
    {
        public static List<RemoteInstanceSetting> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<RemoteInstanceSetting>>(json, Settings);
        }

        public static string ToJson(IList<RemoteInstanceSetting> settings)
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
