namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using Persistence;

    [Handler]
    class DeleteCustomCheckHandler(ICustomChecksDataStore customChecksDataStore, IDomainEvents domainEvents) : IHandleMessages<DeleteCustomCheck>
    {
        public async Task Handle(DeleteCustomCheck message, IMessageHandlerContext context)
        {
            await customChecksDataStore.DeleteCustomCheck(message.Id);

            await domainEvents.Raise(new CustomCheckDeleted { Id = message.Id }, context.CancellationToken);
        }
    }
}