namespace ServiceControl.MessageFailures.Api
{
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using InternalMessages;
    using Nancy;
    using NServiceBus;

    public class RetryMessagesApi : RoutedApi<string>
    {
        public IMessageSession Bus { get; set; }

        protected override async Task<Response> LocalQuery(Request request, string input, string instanceId)
        {
            await Bus.SendLocal<RetryMessage>(m => { m.FailedMessageId = input; })
                .ConfigureAwait(false);

            return new Response {StatusCode = HttpStatusCode.Accepted};
        }
    }
}