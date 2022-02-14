namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class DictionarySerializationExtensions
    {
        public static Dictionary<string, string> FromJson(this string json) => JsonConvert.DeserializeObject<Dictionary<string, string>>(json, settings);

        public static string ToJson(this Dictionary<string, string> dictionary) => JsonConvert.SerializeObject(dictionary, settings);

        static JsonSerializerSettings settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}