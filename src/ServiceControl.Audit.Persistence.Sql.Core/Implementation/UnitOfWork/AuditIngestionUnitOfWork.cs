namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation.UnitOfWork;

using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Abstractions;
using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using NServiceBus;
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
        var contentType = GetContentType(processedMessage.Headers, MediaTypeNames.Text.Plain);

        // Add metadata
        processedMessage.MessageMetadata["ContentLength"] = body.Length;
        if (!body.IsEmpty)
        {
            processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
        }

        // Extract denormalized fields from MessageMetadata
        var entity = new ProcessedMessageEntity
        {
            Id = SequentialGuidGenerator.NewSequentialGuid(),
            UniqueMessageId = processedMessage.UniqueMessageId,
            MessageMetadataJson = JsonSerializer.Serialize(processedMessage.MessageMetadata, JsonSerializationOptions.Default),
            HeadersJson = JsonSerializer.Serialize(processedMessage.Headers, JsonSerializationOptions.Default),
            ProcessedAt = processedMessage.ProcessedAt,

            // Denormalized fields
            MessageId = GetMetadata<string>(processedMessage.MessageMetadata, "MessageId"),
            MessageType = GetMetadata<string>(processedMessage.MessageMetadata, "MessageType"),
            TimeSent = GetMetadata<DateTime?>(processedMessage.MessageMetadata, "TimeSent"),
            IsSystemMessage = GetMetadata<bool>(processedMessage.MessageMetadata, "IsSystemMessage"),
            IsRetried = GetMetadata<bool>(processedMessage.MessageMetadata, "IsRetried"),
            ConversationId = GetMetadata<string>(processedMessage.MessageMetadata, "ConversationId"),
            MessageIntent = (int)(GetMetadata<MessageIntent?>(processedMessage.MessageMetadata, "MessageIntent") ?? MessageIntent.Send),

            // Endpoint details
            SendingEndpointName = GetEndpointName(processedMessage.MessageMetadata, "SendingEndpoint"),
            ReceivingEndpointName = GetEndpointName(processedMessage.MessageMetadata, "ReceivingEndpoint"),

            // Performance metrics
            CriticalTimeTicks = GetMetadata<TimeSpan?>(processedMessage.MessageMetadata, "CriticalTime")?.Ticks,
            ProcessingTimeTicks = GetMetadata<TimeSpan?>(processedMessage.MessageMetadata, "ProcessingTime")?.Ticks,
            DeliveryTimeTicks = GetMetadata<TimeSpan?>(processedMessage.MessageMetadata, "DeliveryTime")?.Ticks,

            // Body
            Body = BuildInlineBody(processedMessage.Headers, body),
            BodySize = body.Length,
            BodyUrl = body.IsEmpty ? null : $"/messages/{processedMessage.Id}/body",
            BodyNotStored = body.Length > settings.MaxBodySizeToStore,

            // Saga info
            InvokedSagasJson = SerializeSagaInfo(processedMessage.MessageMetadata, "InvokedSagas"),
            OriginatesFromSagaJson = SerializeSagaInfo(processedMessage.MessageMetadata, "OriginatesFromSaga"),

            // Retention
            ExpiresAt = DateTime.UtcNow.Add(settings.AuditRetentionPeriod)
        };

        dbContext.ProcessedMessages.Add(entity);

        // Store body in file system if large enough
        if (!body.IsEmpty && body.Length >= LargeObjectHeapThreshold)
        {
            await storageHelper.WriteBodyAsync(processedMessage.UniqueMessageId, body, contentType, cancellationToken).ConfigureAwait(false);
        }
    }

    // NO-OPS per requirements
    public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Ignore concurrency exceptions during ingestion
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

    static string? SerializeSagaInfo(Dictionary<string, object> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value) && value != null)
        {
            return JsonSerializer.Serialize(value, JsonSerializationOptions.Default);
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
