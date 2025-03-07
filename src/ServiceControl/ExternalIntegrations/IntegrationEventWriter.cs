namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;

    class IntegrationEventWriter : IDomainHandler<IDomainEvent>
    {
        public IntegrationEventWriter(IExternalIntegrationRequestsDataStore store, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
        }

        public async Task Handle(IDomainEvent message, CancellationToken cancellationToken)
        {
            var dispatchContexts = eventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message))
                .ToArray();

            if (dispatchContexts.Length == 0)
            {
                return;
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Storing dispatch requests");
            }

            var dispatchRequests = dispatchContexts.Select(dispatchContext => new ExternalIntegrationDispatchRequest
            {
                DispatchContext = dispatchContext
            }).ToList();


            await store.StoreDispatchRequest(dispatchRequests);
        }

        readonly IExternalIntegrationRequestsDataStore store;
        readonly IEnumerable<IEventPublisher> eventPublishers;

        static readonly ILog Logger = LogManager.GetLogger(typeof(IntegrationEventWriter));
    }
}