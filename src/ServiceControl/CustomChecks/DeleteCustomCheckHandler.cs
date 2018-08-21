namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using Raven.Client;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public DeleteCustomCheckHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(DeleteCustomCheck message, IMessageHandlerContext context)
        {
            await store.AsyncDatabaseCommands.DeleteAsync(store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(message.Id, typeof(CustomCheck), false), null)
                .ConfigureAwait(false);

            await domainEvents.Raise(new CustomCheckDeleted {Id = message.Id})
                .ConfigureAwait(false);
        }

        IDocumentStore store;
        IDomainEvents domainEvents;
    }
}