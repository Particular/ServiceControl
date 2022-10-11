namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing;

    class InMemoryFailedAuditStorage : IFailedAuditStorage
    {
        public InMemoryFailedAuditStorage(InMemoryAuditDataStore dataStore) => this.dataStore = dataStore;

        public async Task ProcessFailedMessages(Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
        {
            foreach (var failedMessage in dataStore.failedAuditImports)
            {
                FailedTransportMessage transportMessage = failedMessage.Message;

                await onMessage(transportMessage, (_) => { return Task.CompletedTask; }, cancellationToken).ConfigureAwait(false);
            }

            dataStore.failedAuditImports.Clear();
        }

        public Task Store(dynamic failure)
        {
            dataStore.failedAuditImports.Add(failure);

            return Task.CompletedTask;
        }

        public Task SaveFailedAuditImport(FailedAuditImport message)
        {
            dataStore.failedAuditImports.Add(message);
            return Task.CompletedTask;
        }

        public Task<int> GetFailedAuditsCount()
        {
            return Task.FromResult(dataStore.failedAuditImports.Count);
        }

        InMemoryAuditDataStore dataStore;
    }
}
