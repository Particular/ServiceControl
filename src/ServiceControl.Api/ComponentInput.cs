namespace ServiceControl.Api
{
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public class ComponentInput
    {
        public ComponentInput(IDomainEvents domainEvents, IDocumentStore documentStore)
        {
            DomainEvents = domainEvents;
            DocumentStore = documentStore;
        }

        public IDomainEvents DomainEvents { get; }
        public IDocumentStore DocumentStore { get; }
    }
}