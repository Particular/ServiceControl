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
        readonly string connectionString;

        public SqlStore(string connectionString)
        {
            this.connectionString = connectionString;
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

        public Task StoreFailure(FailedAuditImport failure)
        {
            throw new NotImplementedException();

            /*
            var id = Guid.NewGuid();

            // Write to Raven
            using (var session = store.OpenAsyncSession())
            {
                failure.Id = id;

                await session.StoreAsync(failure)
                    .ConfigureAwait(false);

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
            */
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
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

#pragma warning disable IDE0060 // Remove unused parameter
        public Task StoreAsync(ProcessedMessage processedMessage)
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }

        public Task StoreAsync(SagaSnapshot sagaSnapshot)
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }

        public Task StoreAsync(KnownEndpoint sagaSnapshot)
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }

#pragma warning restore IDE0060 // Remove unused parameter

    }
}