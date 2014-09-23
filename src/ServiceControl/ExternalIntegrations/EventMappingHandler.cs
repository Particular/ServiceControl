namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;

    public class EventMappingHandler : IHandleMessages<IEvent>
    {
        public ISendMessages MessageSender { get; set; }
        public IDocumentSession Session { get; set; }

        public void Handle(IEvent message)
        {
            var messageFailed = message as MessageFailed;
            if (messageFailed == null)
            {
                return;
            }
            var dispatchRequest = new MessageFailedDispatchRequest()
            {
                FailedMessageId = messageFailed.FailedMessageId
            };
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Storing dispatch request for failutre {0}",dispatchRequest.FailedMessageId);
            }
            Session.Store(dispatchRequest);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventMappingHandler));
    }
}