namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;

    public interface IEventPublisher
    {
        bool Handles(IEvent evnt);
        object CreateDispatchContext(IEvent evnt);

        IEnumerable<object> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IDocumentSession session);
    }
}