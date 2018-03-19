namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public interface IEventPublisher
    {
        bool Handles(IDomainEvent @event);
        object CreateDispatchContext(IDomainEvent @event);

        IEnumerable<object> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IDocumentSession session);
    }
}