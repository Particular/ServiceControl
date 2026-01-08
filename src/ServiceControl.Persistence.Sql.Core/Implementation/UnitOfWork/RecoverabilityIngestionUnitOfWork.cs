namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

class RecoverabilityIngestionUnitOfWork(IngestionUnitOfWork parent, FileSystemBodyStorageHelper storageHelper, IServiceProvider serviceProvider) : IRecoverabilityIngestionUnitOfWork
{
    const int MaxProcessingAttempts = 10;
    // large object heap starts above 85000 bytes and not above 85 KB!
    const int LargeObjectHeapThreshold = 85_000;
    static readonly Encoding utf8 = new UTF8Encoding(true, true);

    public async Task RecordFailedProcessingAttempt(MessageContext context, FailedMessage.ProcessingAttempt processingAttempt, List<FailedMessage.FailureGroup> groups)
    {
        T? GetMetadata<T>(string key)
        {
            if (processingAttempt.MessageMetadata.TryGetValue(key, out var value))
            {
                return (T?)value;
            }
            else
            {
                return default;
            }
        }

        var uniqueMessageId = context.Headers.UniqueId();
        var contentType = GetContentType(context.Headers, MediaTypeNames.Text.Plain);

        // Add metadata to the processing attempt
        processingAttempt.MessageMetadata.Add("ContentType", contentType);
        processingAttempt.MessageMetadata.Add("ContentLength", context.Body.Length);
        processingAttempt.MessageMetadata.Add("BodyUrl", $"/messages/{uniqueMessageId}/body");


        // Extract denormalized fields from headers for efficient querying
        var messageType = GetMetadata<string>("MessageType");
        var timeSent = GetMetadata<DateTime>("TimeSent");
        var queueAddress = context.Headers.GetValueOrDefault("NServiceBus.FailedQ");
        var conversationId = GetMetadata<string>("ConversationId");
        var sendingEndpoint = GetMetadata<EndpointDetails>("SendingEndpoint");
        var receivingEndpoint = GetMetadata<EndpointDetails>("ReceivingEndpoint");

        // Check local cache first to avoid database queries when processing in parallel
        var existingMessage = parent.DbContext.FailedMessages.Local
            .FirstOrDefault(fm => fm.UniqueMessageId == uniqueMessageId);

        // If not in local cache, use a separate DbContext to query database
        // This avoids EF Core concurrency detector issues when processing messages in parallel
        if (existingMessage == null)
        {
            using var scope = serviceProvider.CreateScope();
            var readDbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

            var existing = await readDbContext.FailedMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(fm => fm.UniqueMessageId == uniqueMessageId);

            if (existing != null)
            {
                // Attach to the parent context for change tracking
                existingMessage = parent.DbContext.FailedMessages.Attach(existing).Entity;
            }
        }

        List<FailedMessage.ProcessingAttempt> attempts;
        if (existingMessage != null)
        {
            // Merge with existing attempts
            attempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(existingMessage.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [];

            // De-duplicate attempts by AttemptedAt value
            var duplicateIndex = attempts.FindIndex(a => a.AttemptedAt == processingAttempt.AttemptedAt);
            if (duplicateIndex < 0)
            {
                attempts.Add(processingAttempt);
            }

            // Trim to the latest MaxProcessingAttempts
            attempts = [.. attempts
                .OrderBy(a => a.AttemptedAt)
                .TakeLast(MaxProcessingAttempts)];

            // Update the tracked entity
            existingMessage.Status = FailedMessageStatus.Unresolved;
            existingMessage.ProcessingAttemptsJson = JsonSerializer.Serialize(attempts, JsonSerializationOptions.Default);
            existingMessage.FailureGroupsJson = JsonSerializer.Serialize(groups, JsonSerializationOptions.Default);
            existingMessage.HeadersJson = JsonSerializer.Serialize(processingAttempt.Headers, JsonSerializationOptions.Default);
            existingMessage.Query = BuildSearchableText(processingAttempt.Headers, context.Body); // Populate Query for all databases
            existingMessage.PrimaryFailureGroupId = groups.Count > 0 ? groups[0].Id : null;
            existingMessage.MessageId = processingAttempt.MessageId;
            existingMessage.MessageType = messageType;
            existingMessage.TimeSent = timeSent;
            existingMessage.SendingEndpointName = sendingEndpoint?.Name;
            existingMessage.ReceivingEndpointName = receivingEndpoint?.Name;
            existingMessage.ExceptionType = processingAttempt.FailureDetails?.Exception?.ExceptionType;
            existingMessage.ExceptionMessage = processingAttempt.FailureDetails?.Exception?.Message;
            existingMessage.QueueAddress = queueAddress;
            existingMessage.NumberOfProcessingAttempts = attempts.Count;
            existingMessage.LastProcessedAt = processingAttempt.AttemptedAt;
            existingMessage.ConversationId = conversationId;
        }
        else
        {
            // First attempt for this message
            attempts = [processingAttempt];

            // Build the complete entity with all fields
            var failedMessageEntity = new FailedMessageEntity
            {
                Id = SequentialGuidGenerator.NewSequentialGuid(),
                UniqueMessageId = uniqueMessageId,
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttemptsJson = JsonSerializer.Serialize(attempts, JsonSerializationOptions.Default),
                FailureGroupsJson = JsonSerializer.Serialize(groups, JsonSerializationOptions.Default),
                HeadersJson = JsonSerializer.Serialize(processingAttempt.Headers, JsonSerializationOptions.Default),
                Query = BuildSearchableText(processingAttempt.Headers, context.Body), // Populate Query for all databases
                PrimaryFailureGroupId = groups.Count > 0 ? groups[0].Id : null,
                MessageId = processingAttempt.MessageId,
                MessageType = messageType,
                TimeSent = timeSent,
                SendingEndpointName = sendingEndpoint?.Name,
                ReceivingEndpointName = receivingEndpoint?.Name,
                ExceptionType = processingAttempt.FailureDetails?.Exception?.ExceptionType,
                ExceptionMessage = processingAttempt.FailureDetails?.Exception?.Message,
                QueueAddress = queueAddress,
                NumberOfProcessingAttempts = attempts.Count,
                LastProcessedAt = processingAttempt.AttemptedAt,
                ConversationId = conversationId,
            };
            parent.DbContext.FailedMessages.Add(failedMessageEntity);
        }

        // ALWAYS store to filesystem (regardless of size)
        var shouldCompress = context.Body.Length >= parent.Settings.MinBodySizeForCompression;
        await storageHelper.WriteBodyAsync(uniqueMessageId, context.Body, contentType);
    }

    public async Task RecordSuccessfulRetry(string retriedMessageUniqueId)
    {
        // Find the failed message by unique ID
        var failedMessage = await parent.DbContext.FailedMessages
            .FirstOrDefaultAsync(fm => fm.UniqueMessageId == retriedMessageUniqueId);

        if (failedMessage != null)
        {
            // Update its status to Resolved - EF Core tracks this change
            failedMessage.Status = FailedMessageStatus.Resolved;
        }

        // Remove any retry tracking document - query by UniqueMessageId instead since we no longer have the composite pattern
        var failedMsg = await parent.DbContext.FailedMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(fm => fm.UniqueMessageId == retriedMessageUniqueId);

        if (failedMsg != null)
        {
            var retryDocument = await parent.DbContext.FailedMessageRetries
                .FirstOrDefaultAsync(r => r.FailedMessageId == failedMsg.Id.ToString());

            if (retryDocument != null)
            {
                // EF Core tracks this removal
                parent.DbContext.FailedMessageRetries.Remove(retryDocument);
            }
        }
    }


    static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
        => headers.TryGetValue(Headers.ContentType, out var contentType) ? contentType : defaultContentType;

    static string BuildSearchableText(Dictionary<string, string> headers, ReadOnlyMemory<byte> body)
    {
        var parts = new List<string>
        {
            string.Join(" ", headers.Values) // All header values
        };

        var avoidsLargeObjectHeap = body.Length < LargeObjectHeapThreshold;
        var isBinary = headers.IsBinary();
        if (avoidsLargeObjectHeap && !isBinary)
        {
            try
            {
                var bodyString = utf8.GetString(body.Span);
                parts.Add(bodyString);
            }
            catch
            {
                // If it won't decode to text, don't index it
            }
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
