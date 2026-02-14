namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CompositeViews.Messages;
using Entities;
using Infrastructure;
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
        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [];
        var lastAttempt = processingAttempts.LastOrDefault();

        // Extract endpoint details from metadata (stored during ingestion)
        EndpointDetails? sendingEndpoint = null;
        EndpointDetails? receivingEndpoint = null;

        if (lastAttempt?.MessageMetadata != null)
        {
            if (lastAttempt.MessageMetadata.TryGetValue("SendingEndpoint", out var sendingObj) && sendingObj is JsonElement sendingJson)
            {
                sendingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(sendingJson.GetRawText(), JsonSerializationOptions.Default);
            }

            if (lastAttempt.MessageMetadata.TryGetValue("ReceivingEndpoint", out var receivingObj) && receivingObj is JsonElement receivingJson)
            {
                receivingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(receivingJson.GetRawText(), JsonSerializationOptions.Default);
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
        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [];
        var lastAttempt = processingAttempts.LastOrDefault();
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.HeadersJson, JsonSerializationOptions.Default) ?? [];

        // Extract metadata from the last processing attempt (matching RavenDB implementation)
        var metadata = lastAttempt?.MessageMetadata;

        var isSystemMessage = metadata?.TryGetValue("IsSystemMessage", out var isSystem) == true && isSystem is bool b && b;
        var bodySize = metadata?.TryGetValue("ContentLength", out var size) == true && size is int contentLength ? contentLength : 0;
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
                sendingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(sendingJson.GetRawText(), JsonSerializationOptions.Default);
            }

            if (metadata.TryGetValue("ReceivingEndpoint", out var receivingObj) && receivingObj is JsonElement receivingJson)
            {
                receivingEndpoint = JsonSerializer.Deserialize<EndpointDetails>(receivingJson.GetRawText(), JsonSerializationOptions.Default);
            }

            if (metadata.TryGetValue("OriginatesFromSaga", out var sagaObj) && sagaObj is JsonElement sagaJson)
            {
                originatesFromSaga = JsonSerializer.Deserialize<SagaInfo>(sagaJson.GetRawText(), JsonSerializationOptions.Default);
            }

            if (metadata.TryGetValue("InvokedSagas", out var sagasObj) && sagasObj is JsonElement sagasJson)
            {
                invokedSagas = JsonSerializer.Deserialize<List<SagaInfo>>(sagasJson.GetRawText(), JsonSerializationOptions.Default);
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
            IsSystemMessage = isSystemMessage,
            ConversationId = entity.ConversationId,
            Headers = headers.Select(h => new KeyValuePair<string, object>(h.Key, h.Value)),
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
