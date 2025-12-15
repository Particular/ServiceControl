namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CompositeViews.Messages;
using Entities;
using MessageFailures.Api;
using NServiceBus;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.SagaAudit;

partial class ErrorMessageDataStore
{
    internal static IQueryable<FailedMessageEntity> ApplySorting(IQueryable<FailedMessageEntity> query, SortInfo sortInfo)
    {
        if (sortInfo == null || string.IsNullOrWhiteSpace(sortInfo.Sort))
        {
            return query.OrderByDescending(fm => fm.TimeSent);
        }

        var isDescending = sortInfo.Direction == "desc";

        return sortInfo.Sort.ToLower() switch
        {
            "id" or "message_id" => isDescending
                ? query.OrderByDescending(fm => fm.MessageId)
                : query.OrderBy(fm => fm.MessageId),
            "message_type" => isDescending
                ? query.OrderByDescending(fm => fm.MessageType)
                : query.OrderBy(fm => fm.MessageType),
            "critical_time" => isDescending
                ? query.OrderByDescending(fm => fm.CriticalTime)
                : query.OrderBy(fm => fm.CriticalTime),
            "delivery_time" => isDescending
                ? query.OrderByDescending(fm => fm.DeliveryTime)
                : query.OrderBy(fm => fm.DeliveryTime),
            "processing_time" => isDescending
                ? query.OrderByDescending(fm => fm.ProcessingTime)
                : query.OrderBy(fm => fm.ProcessingTime),
            "processed_at" => isDescending
                ? query.OrderByDescending(fm => fm.LastProcessedAt)
                : query.OrderBy(fm => fm.LastProcessedAt),
            "status" => isDescending
                ? query.OrderByDescending(fm => fm.Status)
                : query.OrderBy(fm => fm.Status),
            "time_sent" or _ => isDescending
                ? query.OrderByDescending(fm => fm.TimeSent)
                : query.OrderBy(fm => fm.TimeSent)
        };
    }

    internal static FailedMessageView CreateFailedMessageView(FailedMessageEntity entity)
    {
        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson) ?? [];
        var lastAttempt = processingAttempts.LastOrDefault();

        // Extract endpoint details from metadata (stored during ingestion)
        EndpointDetails? sendingEndpoint = null;
        EndpointDetails? receivingEndpoint = null;

        if (lastAttempt?.MessageMetadata != null)
        {
            if (lastAttempt.MessageMetadata.TryGetValue("SendingEndpoint", out var sendingObj) && sendingObj is JsonElement sendingJson)
            {
                sendingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(sendingJson.GetRawText());
            }

            if (lastAttempt.MessageMetadata.TryGetValue("ReceivingEndpoint", out var receivingObj) && receivingObj is JsonElement receivingJson)
            {
                receivingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(receivingJson.GetRawText());
            }
        }

        return new FailedMessageView
        {
            Id = entity.UniqueMessageId,
            MessageType = entity.MessageType,
            TimeSent = entity.TimeSent,
            IsSystemMessage = false, // Not stored in entity
            Exception = lastAttempt?.FailureDetails?.Exception,
            MessageId = entity.MessageId,
            NumberOfProcessingAttempts = entity.NumberOfProcessingAttempts ?? 0,
            Status = entity.Status,
            SendingEndpoint = sendingEndpoint,
            ReceivingEndpoint = receivingEndpoint,
            QueueAddress = entity.QueueAddress,
            TimeOfFailure = lastAttempt?.FailureDetails?.TimeOfFailure ?? DateTime.MinValue,
            LastModified = entity.LastProcessedAt ?? DateTime.MinValue,
            Edited = false, // Not implemented
            EditOf = null
        };
    }

    internal static MessagesView CreateMessagesView(FailedMessageEntity entity)
    {
        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson) ?? [];
        var lastAttempt = processingAttempts.LastOrDefault();

        // Extract metadata from the last processing attempt (matching RavenDB implementation)
        var metadata = lastAttempt?.MessageMetadata;

        var isSystemMessage = metadata?.TryGetValue("IsSystemMessage", out var isSystem) == true && isSystem is bool b && b;
        var bodySize = metadata?.TryGetValue("ContentLength", out var size) == true && size is int contentLength ? contentLength : 0;
        var criticalTime = metadata?.TryGetValue("CriticalTime", out var ct) == true && ct is JsonElement ctJson && TimeSpan.TryParse(ctJson.GetString(), out var parsedCt) ? parsedCt : TimeSpan.Zero;
        var processingTime = metadata?.TryGetValue("ProcessingTime", out var pt) == true && pt is JsonElement ptJson && TimeSpan.TryParse(ptJson.GetString(), out var parsedPt) ? parsedPt : TimeSpan.Zero;
        var deliveryTime = metadata?.TryGetValue("DeliveryTime", out var dt) == true && dt is JsonElement dtJson && TimeSpan.TryParse(dtJson.GetString(), out var parsedDt) ? parsedDt : TimeSpan.Zero;
        var messageIntent = metadata?.TryGetValue("MessageIntent", out var mi) == true && mi is JsonElement miJson && Enum.TryParse<MessageIntent>(miJson.GetString(), out var parsedMi) ? parsedMi : MessageIntent.Send;

        // Extract endpoint details from metadata (stored during ingestion)
        EndpointDetails? sendingEndpoint = null;
        EndpointDetails? receivingEndpoint = null;
        SagaInfo? originatesFromSaga = null;
        List<SagaInfo>? invokedSagas = null;

        if (metadata != null)
        {
            if (metadata.TryGetValue("SendingEndpoint", out var sendingObj) && sendingObj is JsonElement sendingJson)
            {
                sendingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(sendingJson.GetRawText());
            }

            if (metadata.TryGetValue("ReceivingEndpoint", out var receivingObj) && receivingObj is JsonElement receivingJson)
            {
                receivingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(receivingJson.GetRawText());
            }

            if (metadata.TryGetValue("OriginatesFromSaga", out var sagaObj) && sagaObj is JsonElement sagaJson)
            {
                originatesFromSaga = JsonSerializer.Deserialize<SagaInfo>(sagaJson.GetRawText());
            }

            if (metadata.TryGetValue("InvokedSagas", out var sagasObj) && sagasObj is JsonElement sagasJson)
            {
                invokedSagas = JsonSerializer.Deserialize<List<SagaInfo>>(sagasJson.GetRawText());
            }
        }

        // Calculate status matching RavenDB logic
        var status = entity.Status == FailedMessageStatus.Resolved
            ? MessageStatus.ResolvedSuccessfully
            : entity.Status == FailedMessageStatus.RetryIssued
                ? MessageStatus.RetryIssued
                : entity.Status == FailedMessageStatus.Archived
                    ? MessageStatus.ArchivedFailure
                    : entity.NumberOfProcessingAttempts == 1
                        ? MessageStatus.Failed
                        : MessageStatus.RepeatedFailure;

        return new MessagesView
        {
            Id = entity.UniqueMessageId,
            MessageId = entity.MessageId,
            MessageType = entity.MessageType,
            SendingEndpoint = sendingEndpoint,
            ReceivingEndpoint = receivingEndpoint,
            TimeSent = entity.TimeSent,
            ProcessedAt = entity.LastProcessedAt ?? DateTime.MinValue,
            CriticalTime = criticalTime,
            ProcessingTime = processingTime,
            DeliveryTime = deliveryTime,
            IsSystemMessage = isSystemMessage,
            ConversationId = entity.ConversationId,
            Headers = lastAttempt?.Headers?.Select(h => new KeyValuePair<string, object>(h.Key, h.Value)) ?? [],
            Status = status,
            MessageIntent = messageIntent,
            BodyUrl = $"/api/errors/{entity.UniqueMessageId}/body",
            BodySize = bodySize,
            InvokedSagas = invokedSagas ?? [],
            OriginatesFromSaga = originatesFromSaga,
            InstanceId = null // Not available for failed messages
        };
    }
}
