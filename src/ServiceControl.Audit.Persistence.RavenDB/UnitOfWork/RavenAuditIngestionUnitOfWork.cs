namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.BodyStorage;
    using NServiceBus;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.Json;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.Infrastructure;
    using ServiceControl.SagaAudit;

    class RavenAuditIngestionUnitOfWork(
        BulkInsertOperation bulkInsert,
        CancellationTokenSource timedCancellationSource,
        TimeSpan auditRetentionPeriod,
        IBodyStorage bodyStorage)
        : IAuditIngestionUnitOfWork
    {
        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        {
            var processingStartedTicks = processedMessage.Headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue)
                ? DateTimeOffsetHelper.ToDateTimeOffset(processingStartedValue).UtcDateTime.Ticks
                : DateTime.UtcNow.Ticks;
            processedMessage.Id ??= $"ProcessedMessages-{processingStartedTicks}-{processedMessage.GetProcessingId()}";

            processedMessage.MessageMetadata["ContentLength"] = body.Length;
            if (!body.IsEmpty)
            {
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }

            await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata());

            if (!body.IsEmpty)
            {
                await using var stream = new ReadOnlyStream(body);
                var contentType = processedMessage.Headers.GetValueOrDefault(Headers.ContentType, "text/plain");

                await bodyStorage.Store(processedMessage.Id, contentType, body.Length, stream, cancellationToken);
            }
        }

        MetadataAsDictionary GetExpirationMetadata() =>
            new()
            {
                [Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(auditRetentionPeriod)
            };

        public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
            => bulkInsert.StoreAsync(sagaSnapshot, GetExpirationMetadata());

        bool completed;

        public async Task Complete(CancellationToken cancellationToken = default)
        {
            // Closing the bulk insert flushes its remaining buffered documents.
            await bulkInsert.DisposeAsync();
            completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!completed)
                {
                    // Bulk inserts are not atomic; abort prevents further writes, not earlier ones.
                    try
                    {
                        await bulkInsert.AbortAsync();
                    }
                    finally
                    {
                        await bulkInsert.DisposeAsync();
                    }
                }
            }
            finally
            {
                timedCancellationSource.Dispose();
            }
        }
    }
}