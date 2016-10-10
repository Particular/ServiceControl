namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    public class EventMappingHandler : IHandleMessages<IEvent>
    {
        private readonly IDocumentStore store;
        private readonly IEnumerable<IEventPublisher> eventPublishers;
        private readonly IBus bus;

        public EventMappingHandler(IDocumentStore store, IEnumerable<IEventPublisher> eventPublishers, IBus bus)
        {
            this.store = store;
            this.eventPublishers = eventPublishers;
            this.bus = bus;
        }
        public void Handle(IEvent message)
        {
            var dispatchContexts = eventPublishers
                .Where(p => p.Handles(message))
                .Select(p => p.CreateDispatchContext(message));

            using (var session = store.OpenSession())
            {
                Dispatch(dispatchContexts, session);
            }
        }

        private void Dispatch(IEnumerable<object> dispatchContexts, IDocumentSession session)
        {
            var eventsToBePublished = eventPublishers.SelectMany(p => p.PublishEventsForOwnContexts(dispatchContexts, session));
            foreach (var eventToBePublished in eventsToBePublished)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Publishing external event on the bus.");
                }

                try
                {
                    bus.Publish(eventToBePublished);
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
                    bus.Publish(m);
                }
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventMappingHandler));
    }
}