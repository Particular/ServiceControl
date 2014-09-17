namespace ServiceControl.ExternalIntegrations
{
    using System;
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
            if (!(message is MessageFailed))
            {
                return;
            }
            var storedEvent = new StoredEvent()
            {
                Payload = message,
                Type = message.GetType().FullName,
                RegistrationDate = DateTime.UtcNow
            };
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Storing event for later dispatching");
            }
            Session.Store(storedEvent);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventMappingHandler));
    }
}