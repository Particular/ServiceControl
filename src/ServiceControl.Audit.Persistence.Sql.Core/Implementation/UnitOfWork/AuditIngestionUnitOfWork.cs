namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation.UnitOfWork;

using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Abstractions;
using DbContexts;
using Entities;
using Infrastructure;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence.Monitoring;
using ServiceControl.Audit.Persistence.UnitOfWork;
using ServiceControl.SagaAudit;

class AuditIngestionUnitOfWork(
    AuditDbContextBase dbContext,
    FileSystemBodyStorageHelper storageHelper,
    AuditSqlPersisterSettings settings)
    : IAuditIngestionUnitOfWork
{
    // Large object heap starts above 85000 bytes
    const int LargeObjectHeapThreshold = 85_000;
    static readonly Encoding Utf8 = new UTF8Encoding(true, true);

    public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default)
    {
        var entity = new ProcessedMessageEntity
        {
            UniqueMessageId = processedMessage.UniqueMessageId,
            HeadersJson = JsonSerializer.Serialize(processedMessage.Headers, ProcessedMessageJsonContext.Default.DictionaryStringString),
            ProcessedAt = processedMessage.ProcessedAt,

            // Denormalized fields
            MessageId = GetMetadata<string>(processedMessage.MessageMetadata, "MessageId"),
            MessageType = GetMetadata<string>(processedMessage.MessageMetadata, "MessageType"),
            TimeSent = GetMetadata<DateTime?>(processedMessage.MessageMetadata, "TimeSent"),
            IsSystemMessage = GetMetadata<bool>(processedMessage.MessageMetadata, "IsSystemMessage"),
            Status = (int)(GetMetadata<bool>(processedMessage.MessageMetadata, "IsRetried") ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful),
            ConversationId = GetMetadata<string>(processedMessage.MessageMetadata, "ConversationId"),

            // Endpoint details
            ReceivingEndpointName = GetEndpointName(processedMessage.MessageMetadata, "ReceivingEndpoint"),

            // Performance metrics
            CriticalTimeTicks = GetMetadata<TimeSpan?>(processedMessage.MessageMetadata, "CriticalTime")?.Ticks,
            ProcessingTimeTicks = GetMetadata<TimeSpan?>(processedMessage.MessageMetadata, "ProcessingTime")?.Ticks,
            DeliveryTimeTicks = GetMetadata<TimeSpan?>(processedMessage.MessageMetadata, "DeliveryTime")?.Ticks,

            // Body
            Body = BuildInlineBody(processedMessage.Headers, body),
            BodySize = body.Length,
            BodyUrl = body.IsEmpty ? null : $"/messages/{processedMessage.Id}/body",
            BodyNotStored = body.Length > settings.MaxBodySizeToStore
        };

        dbContext.ProcessedMessages.Add(entity);

        // Store body in file system if large enough
        if (!body.IsEmpty && body.Length < settings.MaxBodySizeToStore)
        {
            var contentType = GetContentType(processedMessage.Headers, MediaTypeNames.Text.Plain);

            await storageHelper.WriteBodyAsync(processedMessage.UniqueMessageId, body, contentType, cancellationToken);
        }
    }

    public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
    {
        var entity = new SagaSnapshotEntity
        {
            SagaId = sagaSnapshot.SagaId,
            SagaType = sagaSnapshot.SagaType,
            StartTime = sagaSnapshot.StartTime,
            FinishTime = sagaSnapshot.FinishTime,
            Endpoint = sagaSnapshot.Endpoint,
            Status = sagaSnapshot.Status,
            InitiatingMessageJson = JsonSerializer.Serialize(sagaSnapshot.InitiatingMessage, SagaSnapshotJsonContext.Default.InitiatingMessage),
            OutgoingMessagesJson = JsonSerializer.Serialize(sagaSnapshot.OutgoingMessages, SagaSnapshotJsonContext.Default.ListResultingMessage),
            ProcessedAt = sagaSnapshot.ProcessedAt,
            StateAfterChange = sagaSnapshot.StateAfterChange,
        };

        dbContext.SagaSnapshots.Add(entity);

        return Task.CompletedTask;
    }

    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        finally
        {
            await dbContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
        => headers.TryGetValue(Headers.ContentType, out var contentType) ? contentType : defaultContentType;

    static T? GetMetadata<T>(Dictionary<string, object> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            // Handle JSON deserialized types
            if (value is JsonElement jsonElement)
            {
                return DeserializeJsonElement<T>(jsonElement);
            }
        }
        return default;
    }

    static T? DeserializeJsonElement<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<T>(JsonSerializationOptions.Default);
        }
        catch
        {
            return default;
        }
    }

    static string? GetEndpointName(Dictionary<string, object> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value))
        {
            if (value is EndpointDetails endpoint)
            {
                return endpoint.Name;
            }

            if (value is JsonElement jsonElement)
            {
                try
                {
                    var endpoint2 = jsonElement.Deserialize<EndpointDetails>(JsonSerializationOptions.Default);
                    return endpoint2?.Name;
                }
                catch
                {
                    return null;
                }
            }
        }
        return null;
    }

    static string? BuildInlineBody(Dictionary<string, string> headers, ReadOnlyMemory<byte> body)
    {
        var avoidsLargeObjectHeap = body.Length < LargeObjectHeapThreshold;
        var isBinary = IsBinaryContent(headers);

        if (avoidsLargeObjectHeap && !isBinary)
        {
            try
            {
                var bodyString = Utf8.GetString(body.Span);
                if (!string.IsNullOrWhiteSpace(bodyString))
                {
                    return bodyString;
                }
            }
            catch
            {
                // If it won't decode to text, don't index it
            }
        }

        return null;
    }

    static bool IsBinaryContent(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue(Headers.ContentType, out var contentType))
        {
            return contentType.Contains("octet-stream") ||
                   contentType.Contains("application/x-") ||
                   contentType.Contains("image/") ||
                   contentType.Contains("audio/") ||
                   contentType.Contains("video/");
        }
        return false;
    }
}
