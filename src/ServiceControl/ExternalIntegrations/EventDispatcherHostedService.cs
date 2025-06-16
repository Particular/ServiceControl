namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Persistence;

    class EventDispatcherHostedService : IHostedService
    {
        public EventDispatcherHostedService(
            IExternalIntegrationRequestsDataStore store,
            IDomainEvents domainEvents,
            IEnumerable<IEventPublisher> eventPublishers,
            IMessageSession messageSession,
            ILogger<EventDispatcherHostedService> logger)
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
            this.domainEvents = domainEvents;
            this.messageSession = messageSession;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            store.Subscribe(TryDispatchEventBatch);

            return Task.CompletedTask;
        }

        async Task TryDispatchEventBatch(object[] allContexts)
        {
            var eventsToBePublished = new List<object>();
            foreach (var publisher in eventPublishers)
            {
                var events = await publisher.PublishEventsForOwnContexts(allContexts);
                eventsToBePublished.AddRange(events);
            }

            foreach (var eventToBePublished in eventsToBePublished)
            {
                logger.LogDebug("Publishing external event on the bus.");

                try
                {
                    await messageSession.Publish(eventToBePublished);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed dispatching external integration event.");

                    var m = new ExternalIntegrationEventFailedToBePublished
                    {
                        EventType = eventToBePublished.GetType()
                    };
                    try
                    {
                        m.Reason = e.GetBaseException().Message;
                    }
                    catch (Exception)
                    {
                        m.Reason = "Failed to retrieve reason!";
                    }

                    await domainEvents.Raise(m);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return store.StopAsync(cancellationToken);
        }

        readonly IMessageSession messageSession;
        readonly IEnumerable<IEventPublisher> eventPublishers;
        readonly IExternalIntegrationRequestsDataStore store;
        readonly IDomainEvents domainEvents;

        readonly ILogger<EventDispatcherHostedService> logger;
    }
}