namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Raven.Client;

    public abstract class EventPublisher<TEvent, TDispatchContext> :IEventPublisher
        where TEvent : IEvent
    {
        public bool Handles(IEvent evnt)
        {
            return evnt is TEvent;
        }

        public object CreateDispatchContext(IEvent evnt)
        {
            return CreateDispatchRequest((TEvent) evnt);
        }

        protected abstract TDispatchContext CreateDispatchRequest(TEvent evnt);

        public IEnumerable<object> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IDocumentSession session)
        {
            return PublishEvents(allContexts.OfType<TDispatchContext>(), session);
        }

        protected abstract IEnumerable<object> PublishEvents(IEnumerable<TDispatchContext> contexts, IDocumentSession session);
    }
}