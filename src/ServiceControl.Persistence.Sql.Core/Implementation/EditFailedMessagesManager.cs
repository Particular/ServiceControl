namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;

class EditFailedMessagesManager(
    IServiceScope scope) : IEditFailedMessagesManager
{
    readonly ServiceControlDbContextBase dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
    string? currentEditingRequestId;
    FailedMessage? currentMessage;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<FailedMessage?> GetFailedMessage(string uniqueMessageId)
    {
        var entity = await dbContext.FailedMessages
            .FirstOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId);

        if (entity == null)
        {
            return null;
        }

        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonOptions) ?? [];
        var failureGroups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(entity.FailureGroupsJson, JsonOptions) ?? [];

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
        var entity = await dbContext.FailedMessages
            .FirstOrDefaultAsync(m => m.Id == Guid.Parse(failedMessage.Id));

        if (entity != null)
        {
            entity.Status = failedMessage.Status;
            entity.ProcessingAttemptsJson = JsonSerializer.Serialize(failedMessage.ProcessingAttempts, JsonOptions);
            entity.FailureGroupsJson = JsonSerializer.Serialize(failedMessage.FailureGroups, JsonOptions);
            entity.PrimaryFailureGroupId = failedMessage.FailureGroups.Count > 0 ? failedMessage.FailureGroups[0].Id : null;

            // Update denormalized fields from last attempt
            var lastAttempt = failedMessage.ProcessingAttempts.LastOrDefault();
            if (lastAttempt != null)
            {
                entity.MessageId = lastAttempt.MessageId;
                entity.MessageType = lastAttempt.Headers?.GetValueOrDefault("NServiceBus.EnclosedMessageTypes");
                entity.TimeSent = lastAttempt.AttemptedAt;
                entity.SendingEndpointName = lastAttempt.Headers?.GetValueOrDefault("NServiceBus.OriginatingEndpoint");
                entity.ReceivingEndpointName = lastAttempt.Headers?.GetValueOrDefault("NServiceBus.ProcessingEndpoint");
                entity.ExceptionType = lastAttempt.FailureDetails?.Exception?.ExceptionType;
                entity.ExceptionMessage = lastAttempt.FailureDetails?.Exception?.Message;
                entity.QueueAddress = lastAttempt.Headers?.GetValueOrDefault("NServiceBus.FailedQ");
                entity.LastProcessedAt = lastAttempt.AttemptedAt;

                // Extract performance metrics from metadata
                entity.CriticalTime = lastAttempt.MessageMetadata?.TryGetValue("CriticalTime", out var ct) == true && ct is TimeSpan ctSpan ? ctSpan : null;
                entity.ProcessingTime = lastAttempt.MessageMetadata?.TryGetValue("ProcessingTime", out var pt) == true && pt is TimeSpan ptSpan ? ptSpan : null;
                entity.DeliveryTime = lastAttempt.MessageMetadata?.TryGetValue("DeliveryTime", out var dt) == true && dt is TimeSpan dtSpan ? dtSpan : null;
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
