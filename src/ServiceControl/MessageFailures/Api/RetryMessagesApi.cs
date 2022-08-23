namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using InternalMessages;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class RetryMessagesApi : RoutedApi<string>
    {
        public RetryMessagesApi(IMessageSession messageSession, Settings settings, Func<HttpClient> httpClientFactory)
        {
            this.messageSession = messageSession;
            Settings = settings;
            HttpClientFactory = httpClientFactory;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, string input, string instanceId)
        {
            await messageSession.SendLocal<RetryMessage>(m => { m.FailedMessageId = input; })
                .ConfigureAwait(false);

            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        readonly IMessageSession messageSession;
    }
}