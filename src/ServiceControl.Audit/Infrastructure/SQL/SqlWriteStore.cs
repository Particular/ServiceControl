namespace ServiceControl.Audit.Infrastructure.SQL
{
    using System;
    using System.Data.SqlClient;
    using System.Linq;
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
                await connection.ExecuteAsync(SqlConstants.CreateHeadersTable).ConfigureAwait(false);
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
                var endpointDetails = processedMessage.MessageMetadata.GetOrDefault<EndpointDetails>("ReceivingEndpoint");

                //HINT: this can be simplified by changing the ingestor
                var processingId = processedMessage.Id.Split('/')[1];

                //HINT: in reality all the values except MessageId and MessageIntent can be null
                await connection.ExecuteAsync(SqlConstants.InsertMessageView,
                    new
                    {
                        Id = processingId,
                        MessageId = (string)processedMessage.MessageMetadata["MessageId"] ?? Guid.NewGuid().ToString(),
                        MessageType = processedMessage.MessageMetadata.GetOrDefault<string>("MessageType"),
                        IsSystemMessage = processedMessage.MessageMetadata.GetOrDefault<bool>("IsSystemMessage"),
                        IsRetried = processedMessage.MessageMetadata.GetOrDefault<bool>("IsRetried"),
                        TimeSent = processedMessage.MessageMetadata.GetOrDefault<DateTime>("TimeSent"),
                        processedMessage.ProcessedAt,
                        EndpointName = endpointDetails.Name,
                        EndpointHostId = endpointDetails.HostId,
                        EndpointHost = endpointDetails.Host,
                        CriticalTime = ((TimeSpan?)processedMessage.MessageMetadata["CriticalTime"])?.Ticks,
                        ProcessingTime = ((TimeSpan?)processedMessage.MessageMetadata["ProcessingTime"])?.Ticks,
                        DeliveryTime = ((TimeSpan?)processedMessage.MessageMetadata["DeliveryTime"])?.Ticks,
                        ConversationId = processedMessage.MessageMetadata.GetOrDefault<string>("ConversationId")

                    }).ConfigureAwait(false);

                //HINT: In Raven, Query property is on the ProcessedMessage. With SQL it's on a dedicated Headers table
                //      Secondly, it does not include Metadata values which in Raven were used to store message body
                await connection.ExecuteAsync(SqlConstants.InsertHeaders,
                    new
                    {
                        ProcessingId = processingId,
                        HeadersText = processedMessage.Headers.ToJson(),
                        Query = string.Join(" ", processedMessage.Headers.Select(x => x.Value))
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