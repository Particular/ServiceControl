namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

class RecoverabilityIngestionUnitOfWork(IngestionUnitOfWork parent) : IRecoverabilityIngestionUnitOfWork
{
    const int MaxProcessingAttempts = 10;

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
        var bodySize = context.Body.Length;

        // Add metadata to the processing attempt
        processingAttempt.MessageMetadata.Add("ContentType", contentType);
        processingAttempt.MessageMetadata.Add("ContentLength", bodySize);
        processingAttempt.MessageMetadata.Add("BodyUrl", $"/messages/{uniqueMessageId}/body");


        // Extract denormalized fields from headers for efficient querying
        var messageType = GetMetadata<string>("MessageType");
        var timeSent = GetMetadata<DateTime>("TimeSent");
        var queueAddress = context.Headers.GetValueOrDefault("NServiceBus.FailedQ");
        var conversationId = GetMetadata<string>("ConversationId");
        var sendingEndpoint = GetMetadata<EndpointDetails>("SendingEndpoint");
        var receivingEndpoint = GetMetadata<EndpointDetails>("ReceivingEndpoint");

        // Load existing message to merge attempts list
        var existingMessage = await parent.DbContext.FailedMessages
            .FirstOrDefaultAsync(fm => fm.UniqueMessageId == uniqueMessageId);

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

        // Store the message body (avoid allocation if body already exists)
        await StoreMessageBody(uniqueMessageId, context.Body, contentType, bodySize);
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

    async Task StoreMessageBody(string uniqueMessageId, ReadOnlyMemory<byte> body, string contentType, int bodySize)
    {
        // Parse the uniqueMessageId to Guid for querying
        var bodyId = Guid.Parse(uniqueMessageId);

        // Check if body already exists (bodies are immutable)
        var exists = await parent.DbContext.MessageBodies
            .AsNoTracking()
            .AnyAsync(mb => mb.Id == bodyId);

        if (!exists)
        {
            // Only allocate the array if we need to store it
            var bodyEntity = new MessageBodyEntity
            {
                Id = bodyId,
                Body = body.ToArray(), // Allocation happens here, but only when needed
                ContentType = contentType,
                BodySize = bodySize,
                Etag = Guid.NewGuid().ToString() // Generate a simple etag
            };

            // Add new message body
            parent.DbContext.MessageBodies.Add(bodyEntity);
        }
        // If body already exists, we don't update it (it's immutable) - no allocation!
    }

    static string GetContentType(IReadOnlyDictionary<string, string> headers, string defaultContentType)
        => headers.TryGetValue(Headers.ContentType, out var contentType) ? contentType : defaultContentType;
}
