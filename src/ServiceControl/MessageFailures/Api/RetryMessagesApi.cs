namespace ServiceControl.MessageFailures.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using InternalMessages;
    using Microsoft.AspNetCore.Http;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    public record RetryMessagesApiContext(string InstanceId, string FailedMessageId) : RoutedApiContext(InstanceId);

    public class RetryMessagesApi : RoutedApi<RetryMessagesApiContext>
    {
        public RetryMessagesApi(IMessageSession messageSession, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
            : base(settings, httpClientFactory, httpContextAccessor)
        {
            this.messageSession = messageSession;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(RetryMessagesApiContext input)
        {
            await messageSession.SendLocal<RetryMessage>(m => { m.FailedMessageId = input.FailedMessageId; });

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        readonly IMessageSession messageSession;
    }
}