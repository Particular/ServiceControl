namespace ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.Audit.Persistence.Infrastructure;
using ServiceControl.Audit.Persistence.UnitOfWork;
using ServiceControl.SagaAudit;

sealed class AuditIngestionUnitOfWork(IServiceScopeFactory scopeFactory, EFPersisterSettings settings, DateTime createdOn) : IAuditIngestionUnitOfWork
{
    readonly List<AuditMessageEntity> messages = [];
    readonly List<SagaSnapshotEntity> snapshots = [];

    public Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default)
    {
        var headers = processedMessage.Headers;
        var metadata = processedMessage.MessageMetadata;
        var uniqueMessageId = ToGuid(processedMessage.UniqueMessageId);
        var (bodyState, bodyText) = MessageBodyClassifier.Classify(headers, body, settings.MaxBodySizeToStore);
        var bodyId = AuditBodyId.Format(createdOn, uniqueMessageId);

        processedMessage.Id ??= bodyId;
        metadata["ContentLength"] = body.Length;
        if (!body.IsEmpty)
        {
            metadata["BodyUrl"] = $"/messages/{bodyId}/body";
        }

        var sendingEndpoint = GetMetadata<EndpointDetails>(metadata, "SendingEndpoint");
        var receivingEndpoint = GetMetadata<EndpointDetails>(metadata, "ReceivingEndpoint");

        messages.Add(new AuditMessageEntity
        {
            CreatedOn = createdOn,
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
            BodyState = bodyState,
            BodySize = body.Length,
            BodyContentType = headers.GetValueOrDefault(Headers.ContentType, "text/plain")
        });

        return Task.CompletedTask;
    }

    public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
    {
        snapshots.Add(new SagaSnapshotEntity
        {
            CreatedOn = createdOn,
            SagaId = sagaSnapshot.SagaId,
            SagaType = sagaSnapshot.SagaType,
            Status = sagaSnapshot.Status,
            StartTime = sagaSnapshot.StartTime,
            FinishTime = sagaSnapshot.FinishTime,
            ProcessedAt = sagaSnapshot.ProcessedAt,
            Endpoint = sagaSnapshot.Endpoint,
            StateAfterChange = sagaSnapshot.StateAfterChange,
            InitiatingMessageJson = SagaSnapshotJson.Write(sagaSnapshot.InitiatingMessage),
            OutgoingMessagesJson = SagaSnapshotJson.Write(sagaSnapshot.OutgoingMessages)
        });

        return Task.CompletedTask;
    }

    public async Task Complete(CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0 && snapshots.Count == 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            await BatchInsert.Rows(dbContext, messages, token);
            await BatchInsert.Rows(dbContext, snapshots, token);
            await transaction.CommitAsync(token);
        }, cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    static Guid ToGuid(string? uniqueMessageId) =>
        uniqueMessageId is null ? Guid.NewGuid()
        : Guid.TryParse(uniqueMessageId, out var parsed) ? parsed
        : DeterministicGuid.MakeId(uniqueMessageId);

    static T? GetMetadata<T>(Dictionary<string, object> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && value is T typed ? typed : default;
}
