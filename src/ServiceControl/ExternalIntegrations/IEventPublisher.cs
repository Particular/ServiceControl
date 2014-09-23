namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;

    public interface IEventPublisher
    {
        bool Handles(IEvent evnt);
        object CreateReference(IEvent evnt);
        IEnumerable<object> PublishEventsForOwnReferences(IEnumerable<object> allReferences, IDocumentSession session);
    }
}