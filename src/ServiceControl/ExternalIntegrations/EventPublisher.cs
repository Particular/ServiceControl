namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Raven.Client;

    public abstract class EventPublisher<TEvent, TReference> :IEventPublisher
        where TEvent : IEvent
    {
        public bool Handles(IEvent evnt)
        {
            return evnt is TEvent;
        }

        public object CreateReference(IEvent evnt)
        {
            return CreateReference((TEvent) evnt);
        }

        protected abstract TReference CreateReference(TEvent evnt);

        public IEnumerable<object> PublishEventsForOwnReferences(IEnumerable<object> allReferences, IDocumentSession session)
        {
            return PublishEvents(allReferences.OfType<TReference>(), session);
        }

        protected abstract IEnumerable<object> PublishEvents(IEnumerable<TReference> references, IDocumentSession session);
    }
}