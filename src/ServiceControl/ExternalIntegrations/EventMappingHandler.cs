namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus;
    using NServiceBus.Transports;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;

    public class EventMappingHandler : IHandleMessages<IEvent>
    {
        public ISendMessages MessageSender { get; set; }
        public IDocumentSession Session { get; set; }

        public void Handle(IEvent message)
        {
            if (!(message is MessageFailed))
            {
                return;
            }
            var storedEvent = new StoredEvent()
            {
                Payload = message,
                Type = message.GetType().FullName
            };
            Session.Store(storedEvent);
        }
    }
}