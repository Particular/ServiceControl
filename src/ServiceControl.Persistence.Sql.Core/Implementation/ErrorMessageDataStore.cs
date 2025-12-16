namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.EventLog;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence;

partial class ErrorMessageDataStore : DataStoreBase, IErrorMessageDataStore
{
    public ErrorMessageDataStore(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public Task<FailedMessage[]> FailedMessagesFetch(Guid[] ids)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entities = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => ids.Contains(fm.Id))
                .ToListAsync();

            return entities.Select(entity => new FailedMessage
            {
                Id = entity.Id.ToString(),
                UniqueMessageId = entity.UniqueMessageId,
                Status = entity.Status,
                ProcessingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [],
                FailureGroups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(entity.FailureGroupsJson, JsonSerializationOptions.Default) ?? []
            }).ToArray();
        });
    }

    public Task StoreFailedErrorImport(FailedErrorImport failure)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = new FailedErrorImportEntity
            {
                Id = Guid.Parse(failure.Id),
                MessageJson = JsonSerializer.Serialize(failure.Message, JsonSerializationOptions.Default),
                ExceptionInfo = failure.ExceptionInfo
            };

            dbContext.FailedErrorImports.Add(entity);
            await dbContext.SaveChangesAsync();
        });
    }

    public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager()
    {
        var scope = serviceProvider.CreateScope();
        var manager = new EditFailedMessagesManager(scope);
        return Task.FromResult<IEditFailedMessagesManager>(manager);
    }

    public Task<INotificationsManager> CreateNotificationsManager()
    {
        var scope = serviceProvider.CreateScope();
        var manager = new NotificationsManager(scope);
        return Task.FromResult<INotificationsManager>(manager);
    }

    public async Task StoreEventLogItem(EventLogItem logItem)
    {
        using var scope = serviceProvider.CreateScope();
        var eventLogDataStore = scope.ServiceProvider.GetRequiredService<IEventLogDataStore>();
        await eventLogDataStore.Add(logItem);
    }

    public Task StoreFailedMessagesForTestsOnly(params FailedMessage[] failedMessages)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            foreach (var failedMessage in failedMessages)
            {
                var lastAttempt = failedMessage.ProcessingAttempts.LastOrDefault();

                var entity = new FailedMessageEntity
                {
                    Id = Guid.Parse(failedMessage.Id),
                    UniqueMessageId = failedMessage.UniqueMessageId,
                    Status = failedMessage.Status,
                    ProcessingAttemptsJson = JsonSerializer.Serialize(failedMessage.ProcessingAttempts, JsonSerializationOptions.Default),
                    FailureGroupsJson = JsonSerializer.Serialize(failedMessage.FailureGroups, JsonSerializationOptions.Default),
                    HeadersJson = JsonSerializer.Serialize(lastAttempt?.Headers ?? [], JsonSerializationOptions.Default),
                    PrimaryFailureGroupId = failedMessage.FailureGroups.Count > 0 ? failedMessage.FailureGroups[0].Id : null,

                    // Extract denormalized fields from last processing attempt if available
                    MessageId = lastAttempt?.MessageId,
                    MessageType = lastAttempt?.Headers?.GetValueOrDefault("NServiceBus.EnclosedMessageTypes"),
                    TimeSent = lastAttempt?.Headers != null && lastAttempt.Headers.TryGetValue("NServiceBus.TimeSent", out var ts) && DateTimeOffset.TryParse(ts, out var parsedTime) ? parsedTime.UtcDateTime : null,
                    SendingEndpointName = lastAttempt?.Headers?.GetValueOrDefault("NServiceBus.OriginatingEndpoint"),
                    ReceivingEndpointName = lastAttempt?.Headers?.GetValueOrDefault("NServiceBus.ProcessingEndpoint"),
                    ExceptionType = lastAttempt?.FailureDetails?.Exception?.ExceptionType,
                    ExceptionMessage = lastAttempt?.FailureDetails?.Exception?.Message,
                    QueueAddress = lastAttempt?.FailureDetails?.AddressOfFailingEndpoint,
                    NumberOfProcessingAttempts = failedMessage.ProcessingAttempts.Count,
                    LastProcessedAt = lastAttempt?.AttemptedAt,
                    ConversationId = lastAttempt?.Headers?.GetValueOrDefault("NServiceBus.ConversationId"),
                };

                dbContext.FailedMessages.Add(entity);
            }

            await dbContext.SaveChangesAsync();
        });
    }

    public Task<byte[]> FetchFromFailedMessage(string uniqueMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var messageBody = await dbContext.MessageBodies
                .AsNoTracking()
                .FirstOrDefaultAsync(mb => mb.Id == Guid.Parse(uniqueMessageId));

            return messageBody?.Body!;
        });
    }
}
