namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public class IntegrationEventWriter : IDomainHandler<IDomainEvent>
    {
        private readonly IDocumentStore store;
        private readonly IEnumerable<IEventPublisher> eventPublishers;

        public IntegrationEventWriter(IDocumentStore store, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
        }
        public void Handle(IDomainEvent message)
        {
            var dispatchContexts = eventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message))
                .ToArray();

            if (dispatchContexts.Length == 0)
            {
                return;
            }

            using (var session = store.OpenSession())
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

                    session.Store(dispatchRequest);
                }

                session.SaveChanges();
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(IntegrationEventWriter));
    }
}