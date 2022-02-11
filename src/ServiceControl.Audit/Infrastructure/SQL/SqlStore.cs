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
        static string connectionString = Environment.GetEnvironmentVariable("PlatformSpike_AzureSQLConnectionString");

        public SqlStore()
        {
        }

        public SqlBulkInsertOperation CreateBulkInsertOperation(bool overrideExistingRows, bool chunked, int chunkSize)
        {
            return new SqlBulkInsertOperation(connectionString);
        }

        public Task RemoveFailedAuditImport(Guid failedImportId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    class SqlBulkInsertOperation
    {
        readonly string connectionString;

        public SqlBulkInsertOperation(string connectionString)
        {
            this.connectionString = connectionString;
        }

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