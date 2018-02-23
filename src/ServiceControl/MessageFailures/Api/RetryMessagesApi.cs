namespace ServiceControl.MessageFailures.Api
{
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RetryMessagesApi : RoutedApi<string>
    {
        public IBus Bus { get; set; }

        protected override Task<Response> LocalQuery(Request request, string input, string instanceId)
        {
            Bus.SendLocal<RetryMessage>(m => { m.FailedMessageId = input; });

            return Task.FromResult(new Response {StatusCode = HttpStatusCode.Accepted});
        }
    }
}