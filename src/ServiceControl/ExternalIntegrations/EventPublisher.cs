namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus;

    public abstract class EventPublisher<TInternalEvent, TPublicEvent> : IHandleMessages<TInternalEvent>
        where TInternalEvent : IEvent
    {
        public IntegrationEventSender EventSender { get; set; }

        public void Handle(TInternalEvent message)
        {
            var publicEvent = Convert(message);
            EventSender.Send(publicEvent);
        }

        protected abstract TPublicEvent Convert(TInternalEvent message);
    }
}