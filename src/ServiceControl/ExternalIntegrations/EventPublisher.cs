namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public abstract class EventPublisher<TEvent, TDispatchContext> :IEventPublisher
        where TEvent : IDomainEvent
    {
        public bool Handles(IDomainEvent @event)
        {
            return @event is TEvent;
        }

        public object CreateDispatchContext(IDomainEvent @event)
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