namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus.Logging;
    using Raven.Client;

    public class IntegrationEventWriter : IDomainHandler<IDomainEvent>
    {
        public IntegrationEventWriter(IDocumentStore store, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
        }

        public async Task Handle(IDomainEvent message)
        {
            var dispatchContexts = eventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message))
                .ToArray();

            if (dispatchContexts.Length == 0)
            {
                return;
            }

            using (var session = store.OpenAsyncSession())
            {
                foreach (var dispatchContext in dispatchContexts)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Storing dispatch request.");
                    }

                    var dispatchRequest = new ExternalIntegrationDispatchRequest
                    {
                        Id = $"ExternalIntegrationDispatchRequests/{Guid.NewGuid()}",
                        DispatchContext = dispatchContext
                    };

                    await session.StoreAsync(dispatchRequest)
                        .ConfigureAwait(false);
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        readonly IDocumentStore store;
        readonly IEnumerable<IEventPublisher> eventPublishers;

        static readonly ILog Logger = LogManager.GetLogger(typeof(IntegrationEventWriter));
    }
}