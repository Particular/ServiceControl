namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using System.Collections.Concurrent;
using NServiceBus;
using ServiceControl.MessageAuditing;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;
using ServiceControl.SagaAudit;

// Record runs concurrently across the batch, so it only adds to thread safe collections and queues
// the external body writes on the parent. Every database call happens in the parent's Complete.
public class EFAuditIngestionUnitOfWork(
    EFIngestionUnitOfWork parentUnitOfWork,
    IBodyStoragePersistence storagePersistence,
    EFPersisterSettings settings,
    DateTime createdOn) : IAuditIngestionUnitOfWork
{
    readonly ConcurrentQueue<AuditMessageEntity> messages = new();
    readonly ConcurrentQueue<SagaSnapshotEntity> snapshots = new();

    /// <summary>
    /// The ingestion hour every row of this batch is stamped with. Fixed when the batch starts so
    /// that a body written during Record and the row written during Complete agree on it.
    /// </summary>
    public DateTime CreatedOn { get; } = createdOn;

    internal IReadOnlyCollection<AuditMessageEntity> Messages => messages;

    internal IReadOnlyCollection<SagaSnapshotEntity> Snapshots => snapshots;

    internal bool IsEmpty => messages.IsEmpty && snapshots.IsEmpty;

    public Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default)
    {
        var uniqueMessageId = Guid.Parse(processedMessage.UniqueMessageId ?? processedMessage.Headers.UniqueId());
        var headers = processedMessage.Headers;
        var metadata = processedMessage.MessageMetadata;
        var contentType = headers.GetValueOrDefault(Headers.ContentType, "text/plain");
        var (bodyText, storeExternally) = MessageBodyClassifier.Classify(headers, body, settings.BodyStorage.MaxBodySizeToStore);

        if (storeExternally)
        {
            parentUnitOfWork.RecordBodyWrite(
                storagePersistence.WriteBody(AuditBodyStorage.BodyId(CreatedOn, uniqueMessageId), body, contentType, cancellationToken));
        }

        var sendingEndpoint = GetMetadata<EndpointDetails>(metadata, "SendingEndpoint");
        var receivingEndpoint = GetMetadata<EndpointDetails>(metadata, "ReceivingEndpoint");

        messages.Enqueue(new AuditMessageEntity
        {
            CreatedOn = CreatedOn,
            UniqueMessageId = uniqueMessageId,
            MessageId = GetMetadata<string>(metadata, "MessageId"),
            MessageType = GetMetadata<string>(metadata, "MessageType"),
            TimeSent = GetMetadata<DateTime?>(metadata, "TimeSent"),
            ProcessedAt = processedMessage.ProcessedAt,
            ConversationId = GetMetadata<string>(metadata, "ConversationId"),
            IsSystemMessage = GetMetadata<bool>(metadata, "IsSystemMessage"),
            Status = GetMetadata<bool>(metadata, "IsRetried") ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
            SendingEndpointName = sendingEndpoint?.Name,
            SendingEndpointHostId = sendingEndpoint?.HostId,
            SendingEndpointHost = sendingEndpoint?.Host,
            ReceivingEndpointName = receivingEndpoint?.Name,
            ReceivingEndpointHostId = receivingEndpoint?.HostId,
            ReceivingEndpointHost = receivingEndpoint?.Host,
            CriticalTimeTicks = GetMetadata<TimeSpan?>(metadata, "CriticalTime")?.Ticks,
            ProcessingTimeTicks = GetMetadata<TimeSpan?>(metadata, "ProcessingTime")?.Ticks,
            DeliveryTimeTicks = GetMetadata<TimeSpan?>(metadata, "DeliveryTime")?.Ticks,
            HeadersJson = MessageHeaders.Write(headers),
            BodyText = bodyText,
            BodyStoredExternally = storeExternally,
            BodySize = body.Length,
            BodyContentType = contentType
        });

        return Task.CompletedTask;
    }

    public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
    {
        snapshots.Enqueue(new SagaSnapshotEntity
        {
            CreatedOn = CreatedOn,
            SagaId = sagaSnapshot.SagaId,
            SagaType = sagaSnapshot.SagaType,
            Status = sagaSnapshot.Status,
            StartTime = AsUtc(sagaSnapshot.StartTime),
            FinishTime = AsUtc(sagaSnapshot.FinishTime),
            ProcessedAt = AsUtc(sagaSnapshot.ProcessedAt),
            Endpoint = sagaSnapshot.Endpoint,
            StateAfterChange = sagaSnapshot.StateAfterChange,
            InitiatingMessageJson = SagaSnapshotJson.Write(sagaSnapshot.InitiatingMessage),
            OutgoingMessagesJson = SagaSnapshotJson.Write(sagaSnapshot.OutgoingMessages)
        });

        return Task.CompletedTask;
    }

    // Saga times arrive deserialized from the saga audit message, where a value without an offset
    // comes back Unspecified. They are UTC on the wire, and PostgreSQL refuses an Unspecified kind.
    static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unknown DateTimeKind")
    };

    static T? GetMetadata<T>(Dictionary<string, object> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && value is T typed ? typed : default;
}
