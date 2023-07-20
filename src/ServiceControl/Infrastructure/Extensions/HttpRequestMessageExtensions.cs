namespace ServiceControl.Persistence.Infrastructure
{
    using System.Net.Http;

    public static class HttpRequestMessageExtensions
    {
        public static string GetStatus(this HttpRequestMessage request)
        {
            return request.GetQueryStringValue<string>("status");
        }

        public static string GetModified(this HttpRequestMessage request)
        {
            return request.GetQueryStringValue<string>("modified");
        }

        public static string GetEndpointName(this HttpRequestMessage request)
        {
            return request.GetQueryStringValue<string>("endpointName");
        }

        public static string GetQueueAddress(this HttpRequestMessage request)
        {
            return request.GetQueryStringValue<string>("queueAddress");
        }

        public static string GetClassifierFilter(this HttpRequestMessage request)
        {
            return request.GetQueryStringValue<string>("classifierFilter");
        }
    }
}