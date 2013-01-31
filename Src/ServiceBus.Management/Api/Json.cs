namespace ServiceBus.Management.Api
{
    using System.Collections.Generic;
    using Raven.Client;
    using global::Nancy;

    public class Json
    {
        public static Response Format(IEnumerable<object> list, RavenQueryStatistics stats)
        {
            var response = (Response)Newtonsoft.Json.JsonConvert.SerializeObject(list);

            response.ContentType = "application/json";
            response.Headers["X-TotalCount"] = stats.TotalResults.ToString();

            return response;
        }
    }
}