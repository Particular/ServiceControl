namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using Persistence;

    class EventDispatcherHostedService : IHostedService
    {
        public EventDispatcherHostedService(
            IExternalIntegrationRequestsDataStore store,
            IDomainEvents domainEvents,
            IEnumerable<IEventPublisher> eventPublishers,
            IMessageSession messageSession
            )
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
            this.domainEvents = domainEvents;
            this.messageSession = messageSession;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            store.Subscribe(TryDispatchEventBatch);

            return Task.FromResult(0);
        }

        async Task TryDispatchEventBatch(object[] allContexts)
        {
            var eventsToBePublished = new List<object>();
            foreach (var publisher in eventPublishers)
            {
                var events = await publisher.PublishEventsForOwnContexts(allContexts)
                    .ConfigureAwait(false);
                eventsToBePublished.AddRange(events);
            }

            foreach (var eventToBePublished in eventsToBePublished)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Publishing external event on the bus.");
                }

                try
                {
                    await messageSession.Publish(eventToBePublished)
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error("Failed dispatching external integration event.", e);

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

                    await domainEvents.Raise(m)
                        .ConfigureAwait(false);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return store.Stop();
        }

        IMessageSession messageSession;
        IEnumerable<IEventPublisher> eventPublishers;
        IExternalIntegrationRequestsDataStore store;
        IDomainEvents domainEvents;

        static ILog Logger = LogManager.GetLogger(typeof(EventDispatcherHostedService));
    }
}