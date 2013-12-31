namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Linq;

    public class IssueRetryAllHandler : IHandleMessages<RequestRetryAll>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(RequestRetryAll message)
        {
            IQueryable<FailedMessage> query = Session.Query<FailedMessage>();

            if (message.Endpoint != null)
            {
                query = Session.Query<FailedMessage>()
                    .Where(
                        fm => fm.MostRecentAttempt.FailureDetails.AddressOfFailingEndpoint.Queue == message.Endpoint);
            }

            using (var ie = Session.Advanced.Stream(query))
            {
                while (ie.MoveNext())
                {
                    var retryMessage = new RetryMessage {FailedMessageId = ie.Current.Document.UniqueMessageId};
                    message.SetHeader("RequestedAt", Bus.CurrentMessageContext.Headers["RequestedAt"]);

                    Bus.SendLocal(retryMessage);
                }
            }
        }
    }
}