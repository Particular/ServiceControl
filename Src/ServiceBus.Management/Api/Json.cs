namespace ServiceBus.Management.Api
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using Raven.Client;
    using global::Nancy;

    public class Json
    {
        public static Response Format(IEnumerable<object> list, RavenQueryStatistics stats)
        {
            var response = (Response)JsonConvert.SerializeObject(list,
                                                                    Formatting.Indented,
                                                                new JsonSerializerSettings { ContractResolver = new LowercaseContractResolver() });

            response.ContentType = "application/json";
            response.Headers["TotalCount"] = stats.TotalResults.ToString();

            return response;
        }

        //todo - we need to exclude the headers from the lowercasing
        public class LowercaseContractResolver : DefaultContractResolver
        {
            protected override string ResolvePropertyName(string propertyName)
            {
                return propertyName.ToLower();
            }
        }
    }
}