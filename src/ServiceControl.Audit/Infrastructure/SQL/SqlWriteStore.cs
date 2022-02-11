namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Dapper;
    using Monitoring;
    using ServiceControl.SagaAudit;

    class SqlWriteStore
    {
        readonly string connectionString;

        public SqlWriteStore(string connectionString)
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

        public async Task Initialize()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(SqlConstants.CreateMessageViewTable).ConfigureAwait(false);
                await connection.ExecuteAsync(SqlConstants.CreateKnownEndpoints).ConfigureAwait(false);
            }
        }
    }

    class SqlBulkInsertOperation
    {
        readonly string connectionString;

        public SqlBulkInsertOperation(string connectionString)
        {
            this.connectionString = connectionString;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public async Task StoreAsync(ProcessedMessage processedMessage)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var endpointDetails = (EndpointDetails)processedMessage.MessageMetadata["ReceivingEndpoint"];

                await connection.ExecuteAsync(SqlConstants.InsertMessageView,
                    new
                    {
                        MessageId = (string)processedMessage.MessageMetadata["MessageId"],
                        MessageType = (string)processedMessage.MessageMetadata["MessageType"],
                        IsSystemMessage = (bool)processedMessage.MessageMetadata["IsSystemMessage"],
                        IsRetried = (bool)processedMessage.MessageMetadata["IsRetried"],
                        TimeSent = (DateTime)processedMessage.MessageMetadata["TimeSent"],
                        processedMessage.ProcessedAt,
                        EndpointName = endpointDetails.Name,
                        EndpointHostId = endpointDetails.HostId,
                        EndpointHost = endpointDetails.Host,
                        CriticalTime = ((TimeSpan?)processedMessage.MessageMetadata["CriticalTime"])?.Ticks,
                        ProcessingTime = ((TimeSpan?)processedMessage.MessageMetadata["ProcessingTime"])?.Ticks,
                        DeliveryTime = ((TimeSpan?)processedMessage.MessageMetadata["DeliveryTime"])?.Ticks,
                        //Query = processedMessage.MessageMetadata.Select(_ => _.Value.ToString()).Union(new[] { string.Join(" ", message.Headers.Select(x => x.Value)) }).ToArray(),
                        ConversationId = (string)processedMessage.MessageMetadata["ConversationId"]

                    }).ConfigureAwait(false);
            }
        }

        public async Task StoreAsync(KnownEndpoint endpoint)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(SqlConstants.UpdateKnownEndpoint, endpoint).ConfigureAwait(false);
            }
        }

        public Task StoreAsync(SagaSnapshot sagaSnapshot) => throw new NotImplementedException();


        public Task DisposeAsync() => Task.CompletedTask;

#pragma warning restore IDE0060 // Remove unused parameter

    }
}