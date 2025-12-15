namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;

public class RetryHistoryDataStore : DataStoreBase, IRetryHistoryDataStore
{
    const int SingletonId = 1;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RetryHistoryDataStore(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public Task<RetryHistory> GetRetryHistory()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.RetryHistory
                .AsNoTracking()
                .FirstOrDefaultAsync(rh => rh.Id == SingletonId);

            if (entity == null)
            {
                return null!;
            }

            var historicOperations = string.IsNullOrEmpty(entity.HistoricOperationsJson)
                ? []
                : JsonSerializer.Deserialize<List<HistoricRetryOperation>>(entity.HistoricOperationsJson, JsonOptions) ?? [];

            var unacknowledgedOperations = string.IsNullOrEmpty(entity.UnacknowledgedOperationsJson)
                ? []
                : JsonSerializer.Deserialize<List<UnacknowledgedRetryOperation>>(entity.UnacknowledgedOperationsJson, JsonOptions) ?? [];

            return new RetryHistory
            {
                Id = RetryHistory.MakeId(),
                HistoricOperations = historicOperations,
                UnacknowledgedOperations = unacknowledgedOperations
            };
        });
    }

    public Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime,
        DateTime completionTime, string originator, string classifier, bool messageFailed,
        int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.RetryHistory.FirstOrDefaultAsync(rh => rh.Id == SingletonId);

            if (entity == null)
            {
                entity = new RetryHistoryEntity { Id = SingletonId };
                await dbContext.RetryHistory.AddAsync(entity);
            }

            // Deserialize existing data
            var historicOperations = string.IsNullOrEmpty(entity.HistoricOperationsJson)
                ? []
                : JsonSerializer.Deserialize<List<HistoricRetryOperation>>(entity.HistoricOperationsJson, JsonOptions) ?? [];

            var unacknowledgedOperations = string.IsNullOrEmpty(entity.UnacknowledgedOperationsJson)
                ? []
                : JsonSerializer.Deserialize<List<UnacknowledgedRetryOperation>>(entity.UnacknowledgedOperationsJson, JsonOptions) ?? [];

            // Add to history (mimicking RetryHistory.AddToHistory)
            var historicOperation = new HistoricRetryOperation
            {
                RequestId = requestId,
                RetryType = retryType,
                StartTime = startTime,
                CompletionTime = completionTime,
                Originator = originator,
                Failed = messageFailed,
                NumberOfMessagesProcessed = numberOfMessagesProcessed
            };

            historicOperations = historicOperations
                .Union(new[] { historicOperation })
                .OrderByDescending(retry => retry.CompletionTime)
                .Take(retryHistoryDepth)
                .ToList();

            // Add to unacknowledged if applicable
            if (retryType is not RetryType.MultipleMessages and not RetryType.SingleMessage)
            {
                var unacknowledgedOperation = new UnacknowledgedRetryOperation
                {
                    RequestId = requestId,
                    RetryType = retryType,
                    StartTime = startTime,
                    CompletionTime = completionTime,
                    Last = lastProcessed,
                    Originator = originator,
                    Classifier = classifier,
                    Failed = messageFailed,
                    NumberOfMessagesProcessed = numberOfMessagesProcessed
                };

                unacknowledgedOperations.Add(unacknowledgedOperation);
            }

            // Serialize and save
            entity.HistoricOperationsJson = JsonSerializer.Serialize(historicOperations, JsonOptions);
            entity.UnacknowledgedOperationsJson = JsonSerializer.Serialize(unacknowledgedOperations, JsonOptions);

            await dbContext.SaveChangesAsync();
        });
    }

    public Task<bool> AcknowledgeRetryGroup(string groupId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.RetryHistory.FirstOrDefaultAsync(rh => rh.Id == SingletonId);

            if (entity == null || string.IsNullOrEmpty(entity.UnacknowledgedOperationsJson))
            {
                return false;
            }

            var unacknowledgedOperations = JsonSerializer.Deserialize<List<UnacknowledgedRetryOperation>>(entity.UnacknowledgedOperationsJson, JsonOptions) ?? [];

            // Find and remove matching operations
            var removed = unacknowledgedOperations.RemoveAll(x =>
                x.Classifier == groupId && x.RetryType == RetryType.FailureGroup);

            if (removed > 0)
            {
                entity.UnacknowledgedOperationsJson = JsonSerializer.Serialize(unacknowledgedOperations, JsonOptions);
                await dbContext.SaveChangesAsync();
                return true;
            }

            return false;
        });
    }
}
