namespace ServiceControl.MessageFailures.Api
{
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.MessageFailures.InternalMessages;

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