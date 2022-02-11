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

#pragma warning disable IDE0060 // Remove unused parameter
        public SqlBulkInsertOperation CreateBulkInsertOperation(bool overrideExistingRows, bool chunked, int chunkSize)
#pragma warning restore IDE0060 // Remove unused parameter
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
#pragma warning disable IDE0052 // Remove unread private members
        readonly string connectionString;
#pragma warning restore IDE0052 // Remove unread private members

        public SqlBulkInsertOperation(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task StoreAsync(ProcessedMessage processedMessage)
        {
            throw new NotImplementedException();
        }

        public Task StoreAsync(SagaSnapshot sagaSnapshot)
        {
            throw new NotImplementedException();
        }

        public Task StoreAsync(KnownEndpoint sagaSnapshot)
        {
            throw new NotImplementedException();
        }

        public Task DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}