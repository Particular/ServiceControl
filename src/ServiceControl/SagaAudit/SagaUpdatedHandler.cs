namespace ServiceControl.SagaAudit
{
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using ServiceControl.Persistence;

    class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public SagaUpdatedHandler(ISagaAuditDataStore store)
        {
            this.store = store;
        }

        public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            var sagaSnapshot = SagaSnapshotFactory.Create(message);

            return store.StoreSnapshot(sagaSnapshot);
        }

        readonly ISagaAuditDataStore store;
    }
}