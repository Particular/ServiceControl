namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;

    public interface IEventPublisher
    {
        bool Handles(IEvent @event);
        object CreateDispatchContext(IEvent @event);

        IEnumerable<object> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IDocumentSession session);
    }
}