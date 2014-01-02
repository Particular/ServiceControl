namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using Api;
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
            var query = Session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>();
            
            if (message.Endpoint != null)
            {
                query = query.Where(fm => fm.ReceivingEndpointName == message.Endpoint);
            }

            using (var ie = Session.Advanced.Stream(query.OfType<FailedMessage>()))
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