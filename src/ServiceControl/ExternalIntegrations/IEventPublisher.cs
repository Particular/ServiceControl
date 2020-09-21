namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Raven.Client.Documents.Session;

    interface IEventPublisher
    {
        bool Handles(IDomainEvent @event);
        object CreateDispatchContext(IDomainEvent @event);

        Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IAsyncDocumentSession session);
    }
}