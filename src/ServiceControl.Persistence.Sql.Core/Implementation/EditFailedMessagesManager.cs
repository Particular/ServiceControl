namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DbContexts;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class EditFailedMessagesManager(
    IServiceScope scope) : IEditFailedMessagesManager
{
    readonly ServiceControlDbContextBase dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
    string? currentEditingRequestId;
    FailedMessage? currentMessage;

    public async Task<FailedMessage?> GetFailedMessage(string uniqueMessageId)
    {
        var entity = await dbContext.FailedMessages
            .FirstOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId);

        if (entity == null)
        {
            return null;
        }

        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [];
        var failureGroups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(entity.FailureGroupsJson, JsonSerializationOptions.Default) ?? [];

        currentMessage = new FailedMessage
        {
            Id = entity.Id.ToString(),
            UniqueMessageId = entity.UniqueMessageId,
            Status = entity.Status,
            ProcessingAttempts = processingAttempts,
            FailureGroups = failureGroups
        };

        return currentMessage;
    }

    public async Task UpdateFailedMessage(FailedMessage failedMessage)
    {
        T? GetMetadata<T>(FailedMessage.ProcessingAttempt lastAttempt, string key)
        {
            if (lastAttempt.MessageMetadata.TryGetValue(key, out var value))
            {
                return (T?)value;
            }
            else
            {
                return default;
            }
        }

        var entity = await dbContext.FailedMessages
            .FirstOrDefaultAsync(m => m.Id == Guid.Parse(failedMessage.Id));

        if (entity != null)
        {
            entity.Status = failedMessage.Status;
            entity.ProcessingAttemptsJson = JsonSerializer.Serialize(failedMessage.ProcessingAttempts, JsonSerializationOptions.Default);
            entity.FailureGroupsJson = JsonSerializer.Serialize(failedMessage.FailureGroups, JsonSerializationOptions.Default);
            entity.PrimaryFailureGroupId = failedMessage.FailureGroups.Count > 0 ? failedMessage.FailureGroups[0].Id : null;

            // Update denormalized fields from last attempt
            var lastAttempt = failedMessage.ProcessingAttempts.LastOrDefault();
            if (lastAttempt != null)
            {
                entity.HeadersJson = JsonSerializer.Serialize(lastAttempt.Headers, JsonSerializationOptions.Default);
                var messageType = GetMetadata<string>(lastAttempt, "MessageType");
                var sendingEndpoint = GetMetadata<EndpointDetails>(lastAttempt, "SendingEndpoint");
                var receivingEndpoint = GetMetadata<EndpointDetails>(lastAttempt, "ReceivingEndpoint");

                entity.MessageId = lastAttempt.MessageId;
                entity.MessageType = messageType;
                entity.TimeSent = lastAttempt.AttemptedAt;
                entity.SendingEndpointName = sendingEndpoint?.Name;
                entity.ReceivingEndpointName = receivingEndpoint?.Name;
                entity.ExceptionType = lastAttempt.FailureDetails?.Exception?.ExceptionType;
                entity.ExceptionMessage = lastAttempt.FailureDetails?.Exception?.Message;
                entity.QueueAddress = lastAttempt.Headers?.GetValueOrDefault("NServiceBus.FailedQ");
                entity.LastProcessedAt = lastAttempt.AttemptedAt;
            }

            entity.NumberOfProcessingAttempts = failedMessage.ProcessingAttempts.Count;
        }
    }

    public Task<string?> GetCurrentEditingRequestId(string failedMessageId)
    {
        // Simple in-memory tracking for the editing request
        return Task.FromResult(currentMessage?.Id == failedMessageId ? currentEditingRequestId : null);
    }

    public Task SetCurrentEditingRequestId(string editingMessageId)
    {
        currentEditingRequestId = editingMessageId;
        return Task.CompletedTask;
    }

    public async Task SetFailedMessageAsResolved()
    {
        if (currentMessage != null)
        {
            currentMessage.Status = FailedMessageStatus.Resolved;
            await UpdateFailedMessage(currentMessage);
        }
    }

    public async Task SaveChanges()
    {
        await dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
