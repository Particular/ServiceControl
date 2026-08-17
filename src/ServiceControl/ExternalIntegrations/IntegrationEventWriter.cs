namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Persistence;

    class IntegrationEventWriter(
        IExternalIntegrationRequestsDataStore store,
        IEnumerable<IEventPublisher> eventPublishers,
        ILogger<IntegrationEventWriter> logger) : IDomainHandler<IDomainEvent>
    {
        public async Task Handle(IDomainEvent message, CancellationToken cancellationToken = default)
        {
            var dispatchContexts = eventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message))
                .ToArray();

            if (dispatchContexts.Length == 0)
            {
                return;
            }

            logger.LogDebug("Storing dispatch requests");

            var dispatchRequests = dispatchContexts.Select(dispatchContext => new ExternalIntegrationDispatchRequest
            {
                DispatchContext = dispatchContext
            }).ToList();

            await store.StoreDispatchRequest(dispatchRequests, cancellationToken);
        }
    }
}