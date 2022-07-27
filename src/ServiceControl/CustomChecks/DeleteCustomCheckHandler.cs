namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using Persistence;
    using Raven.Client;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public DeleteCustomCheckHandler(ICustomChecksDataStore customChecksDataStore, IDomainEvents domainEvents)
        {
            this.customChecksDataStore = customChecksDataStore;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(DeleteCustomCheck message, IMessageHandlerContext context)
        {
            await customChecksDataStore.DeleteCustomCheck(message.Id)
                .ConfigureAwait(false);

            await domainEvents.Raise(new CustomCheckDeleted { Id = message.Id })
                .ConfigureAwait(false);
        }

        ICustomChecksDataStore customChecksDataStore;
        IDomainEvents domainEvents;
    }
}