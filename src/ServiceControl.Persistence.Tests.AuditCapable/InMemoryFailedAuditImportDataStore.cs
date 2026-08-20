namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    class InMemoryFailedAuditImportDataStore(InMemoryAuditStore auditStore) : IFailedAuditImportDataStore
    {
        public Task StoreFailedAuditImport(FailedAuditImport failure, CancellationToken cancellationToken = default)
        {
            auditStore.Record(failure);
            return Task.CompletedTask;
        }

        public async Task ProcessFailedAuditImports(Func<FailedTransportMessage, CancellationToken, Task> processMessage, CancellationToken cancellationToken = default)
        {
            foreach (var failedImport in auditStore.FailedImports)
            {
                if (failedImport.Message is null)
                {
                    continue;
                }

                await processMessage(failedImport.Message, cancellationToken);
                auditStore.RemoveFailedImport(failedImport.Id);
            }
        }

        public Task<bool> QueryContainsFailedImports(CancellationToken cancellationToken = default) =>
            Task.FromResult(auditStore.FailedImports.Count > 0);
    }
}
