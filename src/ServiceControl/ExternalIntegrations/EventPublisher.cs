namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Raven.Client;

    public abstract class EventPublisher<TEvent, TDispatchContext> :IEventPublisher
        where TEvent : IEvent
    {
        public bool Handles(IEvent @event)
        {
            return @event is TEvent;
        }

        public object CreateDispatchContext(IEvent @event)
        {
            return CreateDispatchRequest((TEvent)@event);
        }

        protected abstract TDispatchContext CreateDispatchRequest(TEvent @event);

        public IEnumerable<object> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IDocumentSession session)
        {
            return PublishEvents(allContexts.OfType<TDispatchContext>(), session);
        }

        protected abstract IEnumerable<object> PublishEvents(IEnumerable<TDispatchContext> contexts, IDocumentSession session);
    }
}