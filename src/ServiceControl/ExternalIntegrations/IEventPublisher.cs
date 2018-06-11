namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public interface IEventPublisher
    {
        bool Handles(IDomainEvent @event);
        object CreateDispatchContext(IDomainEvent @event);

        Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IAsyncDocumentSession session);
    }
}