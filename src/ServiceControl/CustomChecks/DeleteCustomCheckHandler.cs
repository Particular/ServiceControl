namespace ServiceControl.CustomChecks
{
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;

        public DeleteCustomCheckHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void Handle(DeleteCustomCheck message)
        {
            store.DatabaseCommands.Delete(store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(message.Id, typeof(CustomCheck), false), null);

            domainEvents.Raise(new CustomCheckDeleted { Id = message.Id });
        }
    }
}