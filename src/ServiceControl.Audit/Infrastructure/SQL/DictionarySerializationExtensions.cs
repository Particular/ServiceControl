namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class DictionarySerializationExtensions
    {
        public static IEnumerable<KeyValuePair<string, object>> FromJson(this string json) => JsonConvert.DeserializeObject<IEnumerable<KeyValuePair<string, object>>>(json, settings);

        public static string ToJson(this Dictionary<string, string> dictionary) => JsonConvert.SerializeObject(dictionary.ToArray(), settings);

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