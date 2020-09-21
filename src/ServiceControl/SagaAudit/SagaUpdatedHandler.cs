namespace ServiceControl.SagaAudit
{
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using Raven.Client.Documents;

    class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public SagaUpdatedHandler(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            var sagaSnapshot = SagaSnapshotFactory.Create(message);

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(sagaSnapshot).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        readonly IDocumentStore store;
    }
}