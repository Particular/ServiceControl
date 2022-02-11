namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
    using ServiceControl.SagaAudit;

    class SqlStore
    {
        public SqlBulkInsertOperation CreateBulkInsertOperation(bool overrideExistingRows, bool chunked, int chunkSize)
        {
            throw new NotImplementedException();
        }

        public Task RemoveFailedAuditImport(Guid failedImportId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    class SqlBulkInsertOperation
    {
        public Task StoreAsync(ProcessedMessage processedMessage)
        {
            throw new System.NotImplementedException();
        }

        public Task StoreAsync(SagaSnapshot sagaSnapshot)
        {
            throw new System.NotImplementedException();
        }

        public Task StoreAsync(KnownEndpoint sagaSnapshot)
        {
            throw new System.NotImplementedException();
        }

        public Task DisposeAsync()
        {
            throw new System.NotImplementedException();
        }
    }
}