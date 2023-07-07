namespace ServiceControl.Persistence.Infrastructure
{
    using System.Net.Http;

    public static class IncludeSystemMessageExtension
    {
        public static bool GetIncludeSystemMessages(this HttpRequestMessage request)
        {
            var includeSystemMessages = request.GetQueryStringValue("include_system_messages", false);

            return includeSystemMessages;
        }
    }
}