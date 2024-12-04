namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Auditing;

    class InMemoryFailedAuditStorage(InMemoryAuditDataStore dataStore) : IFailedAuditStorage
    {
        public async Task ProcessFailedMessages(Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
        {
            foreach (var failedMessage in dataStore.failedAuditImports)
            {
                FailedTransportMessage transportMessage = failedMessage.Message;

                await onMessage(transportMessage, _ => Task.CompletedTask, cancellationToken);
            }

            dataStore.failedAuditImports.Clear();
        }

        public Task SaveFailedAuditImport(FailedAuditImport message)
        {
            dataStore.failedAuditImports.Add(message);
            return Task.CompletedTask;
        }

        public Task<int> GetFailedAuditsCount() => Task.FromResult(dataStore.failedAuditImports.Count);
    }
}