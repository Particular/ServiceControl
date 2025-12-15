namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System;
using System.Collections.Generic;
using System.Linq;
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
        var uniqueMessageId = context.Headers.UniqueId();
        var contentType = GetContentType(context.Headers, "text/plain");
        var bodySize = context.Body.Length;

        // Add metadata to the processing attempt
        processingAttempt.MessageMetadata.Add("ContentType", contentType);
        processingAttempt.MessageMetadata.Add("ContentLength", bodySize);
        processingAttempt.MessageMetadata.Add("BodyUrl", $"/messages/{uniqueMessageId}/body");

        // Store endpoint details in metadata for efficient retrieval
        var sendingEndpoint = ExtractSendingEndpoint(context.Headers);
        var receivingEndpoint = ExtractReceivingEndpoint(context.Headers);

        if (sendingEndpoint != null)
        {
            processingAttempt.MessageMetadata.Add("SendingEndpoint", sendingEndpoint);
        }

        if (receivingEndpoint != null)
        {
            processingAttempt.MessageMetadata.Add("ReceivingEndpoint", receivingEndpoint);
        }

        // Extract denormalized fields from headers for efficient querying
        var messageType = context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var mt) ? mt?.Split(',').FirstOrDefault()?.Trim() : null;
        var timeSent = context.Headers.TryGetValue(Headers.TimeSent, out var ts) && DateTimeOffset.TryParse(ts, out var parsedTime) ? parsedTime.UtcDateTime : (DateTime?)null;
        var queueAddress = context.Headers.TryGetValue("NServiceBus.FailedQ", out var qa) ? qa : null;
        var conversationId = context.Headers.TryGetValue(Headers.ConversationId, out var cid) ? cid : null;

        // Extract performance metrics from metadata for efficient sorting
        var criticalTime = processingAttempt.MessageMetadata.TryGetValue("CriticalTime", out var ct) && ct is TimeSpan ctSpan ? (TimeSpan?)ctSpan : null;
        var processingTime = processingAttempt.MessageMetadata.TryGetValue("ProcessingTime", out var pt) && pt is TimeSpan ptSpan ? (TimeSpan?)ptSpan : null;
        var deliveryTime = processingAttempt.MessageMetadata.TryGetValue("DeliveryTime", out var dt) && dt is TimeSpan dtSpan ? (TimeSpan?)dtSpan : null;

        // Load existing message to merge attempts list
        var existingMessage = await parent.DbContext.FailedMessages
            .FirstOrDefaultAsync(fm => fm.UniqueMessageId == uniqueMessageId);

        List<FailedMessage.ProcessingAttempt> attempts;
        if (existingMessage != null)
        {
            // Merge with existing attempts
            attempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(existingMessage.ProcessingAttemptsJson) ?? [];

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
            existingMessage.ProcessingAttemptsJson = JsonSerializer.Serialize(attempts);
            existingMessage.FailureGroupsJson = JsonSerializer.Serialize(groups);
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
            existingMessage.CriticalTime = criticalTime;
            existingMessage.ProcessingTime = processingTime;
            existingMessage.DeliveryTime = deliveryTime;
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
                ProcessingAttemptsJson = JsonSerializer.Serialize(attempts),
                FailureGroupsJson = JsonSerializer.Serialize(groups),
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
                CriticalTime = criticalTime,
                ProcessingTime = processingTime,
                DeliveryTime = deliveryTime
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

    static EndpointDetails? ExtractSendingEndpoint(IReadOnlyDictionary<string, string> headers)
    {
        var endpoint = new EndpointDetails();

        if (headers.TryGetValue("NServiceBus.OriginatingEndpoint", out var name))
        {
            endpoint.Name = name;
        }

        if (headers.TryGetValue("NServiceBus.OriginatingMachine", out var host))
        {
            endpoint.Host = host;
        }

        if (headers.TryGetValue("NServiceBus.OriginatingHostId", out var hostId) && Guid.TryParse(hostId, out var parsedHostId))
        {
            endpoint.HostId = parsedHostId;
        }

        return !string.IsNullOrEmpty(endpoint.Name) ? endpoint : null;
    }

    static EndpointDetails? ExtractReceivingEndpoint(IReadOnlyDictionary<string, string> headers)
    {
        var endpoint = new EndpointDetails();

        if (headers.TryGetValue("NServiceBus.ProcessingEndpoint", out var name))
        {
            endpoint.Name = name;
        }

        if (headers.TryGetValue("NServiceBus.HostDisplayName", out var host))
        {
            endpoint.Host = host;
        }
        else if (headers.TryGetValue("NServiceBus.ProcessingMachine", out var machine))
        {
            endpoint.Host = machine;
        }

        if (headers.TryGetValue("NServiceBus.HostId", out var hostId) && Guid.TryParse(hostId, out var parsedHostId))
        {
            endpoint.HostId = parsedHostId;
        }

        return !string.IsNullOrEmpty(endpoint.Name) ? endpoint : null;
    }
}
