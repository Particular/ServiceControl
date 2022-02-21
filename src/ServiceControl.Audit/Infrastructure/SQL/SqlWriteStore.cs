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
#pragma warning disable IDE0060 // Remove unused parameter

    class SqlBulkInsertOperation
    {
        string connectionString;
        DynamicParameters parameters;

        public SqlBulkInsertOperation(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task StoreAsync(ProcessedMessage processedMessage)
        {
            var endpointDetails = processedMessage.MessageMetadata.GetOrDefault<EndpointDetails>("ReceivingEndpoint");

            //HINT: this can be simplified by changing the ingestor
            var processingId = DeterministicGuid.MakeId(processedMessage.Id.Split('/')[1]);
            var messageId = processedMessage.MessageMetadata["MessageId"];

            //HINT: in reality all the values except MessageId and MessageIntent can be null
            parameters = new DynamicParameters(new
            {
                MV_MessageId = messageId,
                MV_MessageType = processedMessage.MessageMetadata.GetOrDefault<string>("MessageType") ?? string.Empty,
                MV_IsSystemMessage = processedMessage.MessageMetadata.GetOrDefault<bool>("IsSystemMessage"),
                MV_IsRetried = processedMessage.MessageMetadata.GetOrDefault<bool>("IsRetried"),
                MV_TimeSent = processedMessage.MessageMetadata.TryGetValue("TimeSent", out var timeSent) ? (DateTime)timeSent : DateTime.Now,
                MV_ProcessedAt = processedMessage.ProcessedAt,
                MV_EndpointName = endpointDetails.Name,
                MV_EndpointHostId = endpointDetails.HostId,
                MV_EndpointHost = endpointDetails.Host,
                MV_CriticalTime = ((TimeSpan?)processedMessage.MessageMetadata["CriticalTime"])?.Ticks,
                MV_ProcessingTime = ((TimeSpan?)processedMessage.MessageMetadata["ProcessingTime"])?.Ticks,
                MV_DeliveryTime = ((TimeSpan?)processedMessage.MessageMetadata["DeliveryTime"])?.Ticks,
                MV_ConversationId = processedMessage.MessageMetadata.GetOrDefault<string>("ConversationId") ?? string.Empty,

                //HINT: In Raven, Query property is on the ProcessedMessage. With SQL it's on a dedicated Headers table
                //      Secondly, it does not include Metadata values which in Raven were used to store message body
                MH_ProcessingId = processingId,
                MH_MessageId = messageId,
                MH_HeadersText = processedMessage.Headers.ToJson(),
                MH_Query = string.Join(" ", processedMessage.Headers.Select(x => x.Value))
            });

            return Task.CompletedTask;
        }

        public Task StoreAsync(KnownEndpoint endpoint)
        {
            parameters.AddDynamicParams(new
            {
                KE_Id = endpoint.Id,
                KE_Name = endpoint.Name,
                KE_HostId = endpoint.HostId,
                KE_Host = endpoint.Host,
                KE_LastSeen = endpoint.LastSeen
            });

            return Task.CompletedTask;
        }

        public Task StoreAsync(SagaSnapshot sagaSnapshot) => throw new NotImplementedException();


        public async Task DisposeAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(SqlConstants.BatchMessageInsert, parameters).ConfigureAwait(false);
            }
        }
    }
}