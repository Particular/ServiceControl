namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        public DeleteCustomCheckHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(DeleteCustomCheck message, IMessageHandlerContext context)
        {
            using (var session = store.OpenSession())
            {
                await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(CustomCheck.MakeDocumentId(message.Id), null), session.Advanced.Context)
                    .ConfigureAwait(false);
            }

            await domainEvents.Raise(new CustomCheckDeleted {Id = message.Id})
                .ConfigureAwait(false);
        }

        IDocumentStore store;
        IDomainEvents domainEvents;
    }
}