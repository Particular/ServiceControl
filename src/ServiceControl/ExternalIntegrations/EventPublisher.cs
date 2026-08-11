namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    abstract class EventPublisher<TEvent, TDispatchContext> : IEventPublisher
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

        public Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts, CancellationToken cancellationToken = default)
        {
            return PublishEvents(allContexts.OfType<TDispatchContext>(), cancellationToken);
        }

        protected abstract TDispatchContext CreateDispatchRequest(TEvent @event);

        protected abstract Task<IEnumerable<object>> PublishEvents(IEnumerable<TDispatchContext> contexts, CancellationToken cancellationToken = default);
    }
}