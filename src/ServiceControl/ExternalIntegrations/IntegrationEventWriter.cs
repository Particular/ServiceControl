namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;

    class IntegrationEventWriter : IDomainHandler<IDomainEvent>
    {
        public IntegrationEventWriter(IEventDispatcher dispatcher, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.dispatcher = dispatcher;
            this.eventPublishers = eventPublishers;
        }

        public Task Handle(IDomainEvent message)
        {
            var dispatchContexts = eventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message))
                .ToArray();

            return dispatcher.Enqueue(dispatchContexts);
        }

        readonly IEventDispatcher dispatcher;
        readonly IEnumerable<IEventPublisher> eventPublishers;
    }
}