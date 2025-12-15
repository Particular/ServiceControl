namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DbContexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Recoverability;

class RetryBatchesManager(
    IServiceScope scope,
    ILogger logger) : IRetryBatchesManager
{
    readonly ServiceControlDbContextBase dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
    readonly List<Action> deferredActions = [];

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public void Delete(RetryBatch retryBatch)
    {
        deferredActions.Add(() =>
        {
            var entity = dbContext.RetryBatches.Local.FirstOrDefault(e => e.Id == Guid.Parse(retryBatch.Id));
            if (entity == null)
            {
                entity = new RetryBatchEntity { Id = Guid.Parse(retryBatch.Id) };
                dbContext.RetryBatches.Attach(entity);
            }
            dbContext.RetryBatches.Remove(entity);
        });
    }

    public void Delete(RetryBatchNowForwarding forwardingBatch)
    {
        deferredActions.Add(() =>
        {
            var entity = dbContext.RetryBatchNowForwarding.Local.FirstOrDefault(e => e.Id == RetryBatchNowForwardingEntity.SingletonId);
            if (entity == null)
            {
                entity = new RetryBatchNowForwardingEntity { Id = RetryBatchNowForwardingEntity.SingletonId };
                dbContext.RetryBatchNowForwarding.Attach(entity);
            }
            dbContext.RetryBatchNowForwarding.Remove(entity);
        });
    }

    public async Task<FailedMessageRetry[]> GetFailedMessageRetries(IList<string> stagingBatchFailureRetries)
    {
        var retryGuids = stagingBatchFailureRetries.Select(Guid.Parse).ToList();
        var entities = await dbContext.FailedMessageRetries
            .AsNoTracking()
            .Where(e => retryGuids.Contains(e.Id))
            .ToArrayAsync();

        return entities.Select(ToFailedMessageRetry).ToArray();
    }

    public void Evict(FailedMessageRetry failedMessageRetry)
    {
        var entity = dbContext.FailedMessageRetries.Local.FirstOrDefault(e => e.Id == Guid.Parse(failedMessageRetry.Id));
        if (entity != null)
        {
            dbContext.Entry(entity).State = EntityState.Detached;
        }
    }

    public async Task<FailedMessage[]> GetFailedMessages(Dictionary<string, FailedMessageRetry>.KeyCollection keys)
    {
        var messageGuids = keys.Select(Guid.Parse).ToList();
        var entities = await dbContext.FailedMessages
            .AsNoTracking()
            .Where(e => messageGuids.Contains(e.Id))
            .ToArrayAsync();

        return entities.Select(ToFailedMessage).ToArray();
    }

    public async Task<RetryBatchNowForwarding?> GetRetryBatchNowForwarding()
    {
        var entity = await dbContext.RetryBatchNowForwarding
            .FirstOrDefaultAsync(e => e.Id == RetryBatchNowForwardingEntity.SingletonId);

        if (entity == null)
        {
            return null;
        }

        // Pre-load the related retry batch for the "Include" pattern
        if (!string.IsNullOrEmpty(entity.RetryBatchId))
        {
            await dbContext.RetryBatches
                .FirstOrDefaultAsync(b => b.Id == Guid.Parse(entity.RetryBatchId));
        }

        return new RetryBatchNowForwarding
        {
            RetryBatchId = entity.RetryBatchId
        };
    }

    public async Task<RetryBatch?> GetRetryBatch(string retryBatchId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.RetryBatches
            .FirstOrDefaultAsync(e => e.Id == Guid.Parse(retryBatchId), cancellationToken);

        return entity != null ? ToRetryBatch(entity) : null;
    }

    public async Task<RetryBatch?> GetStagingBatch()
    {
        var entity = await dbContext.RetryBatches
            .FirstOrDefaultAsync(b => b.Status == RetryBatchStatus.Staging);

        if (entity == null)
        {
            return null;
        }

        // Pre-load the related failure retries for the "Include" pattern
        var failureRetries = JsonSerializer.Deserialize<List<string>>(entity.FailureRetriesJson, JsonOptions) ?? [];
        if (failureRetries.Count > 0)
        {
            var retryGuids = failureRetries.Select(Guid.Parse).ToList();
            await dbContext.FailedMessageRetries
                .AsNoTracking()
                .Where(f => retryGuids.Contains(f.Id))
                .ToListAsync();
        }

        return ToRetryBatch(entity);
    }

    public async Task Store(RetryBatchNowForwarding retryBatchNowForwarding)
    {
        var entity = await dbContext.RetryBatchNowForwarding
            .FirstOrDefaultAsync(e => e.Id == RetryBatchNowForwardingEntity.SingletonId);

        if (entity == null)
        {
            entity = new RetryBatchNowForwardingEntity
            {
                Id = RetryBatchNowForwardingEntity.SingletonId,
                RetryBatchId = retryBatchNowForwarding.RetryBatchId
            };
            await dbContext.RetryBatchNowForwarding.AddAsync(entity);
        }
        else
        {
            entity.RetryBatchId = retryBatchNowForwarding.RetryBatchId;
        }
    }

    public async Task<MessageRedirectsCollection> GetOrCreateMessageRedirectsCollection()
    {
        var entity = await dbContext.MessageRedirects
            .FirstOrDefaultAsync(e => e.Id == Guid.Parse(MessageRedirectsCollection.DefaultId));

        if (entity != null)
        {
            var collection = JsonSerializer.Deserialize<MessageRedirectsCollection>(entity.RedirectsJson, JsonOptions)
                ?? new MessageRedirectsCollection();

            // Set metadata properties (ETag and LastModified are not available in EF Core the same way as RavenDB)
            // We'll use a timestamp approach instead
            collection.LastModified = entity.LastModified;

            return collection;
        }

        return new MessageRedirectsCollection();
    }

    public Task CancelExpiration(FailedMessage failedMessage)
    {
        // Expiration is handled differently in SQL - we'll implement expiration via a scheduled job
        // For now, this is a no-op in the manager
        logger.LogDebug("CancelExpiration called for message {MessageId} - SQL expiration managed separately", failedMessage.Id);
        return Task.CompletedTask;
    }

    public async Task SaveChanges()
    {
        // Execute any deferred delete actions
        foreach (var action in deferredActions)
        {
            action();
        }
        deferredActions.Clear();

        await dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    static RetryBatch ToRetryBatch(RetryBatchEntity entity)
    {
        return new RetryBatch
        {
            Id = entity.Id.ToString(),
            Context = entity.Context,
            RetrySessionId = entity.RetrySessionId,
            RequestId = entity.RequestId,
            StagingId = entity.StagingId,
            Originator = entity.Originator,
            Classifier = entity.Classifier,
            StartTime = entity.StartTime,
            Last = entity.Last,
            InitialBatchSize = entity.InitialBatchSize,
            Status = entity.Status,
            RetryType = entity.RetryType,
            FailureRetries = JsonSerializer.Deserialize<List<string>>(entity.FailureRetriesJson, JsonOptions) ?? []
        };
    }

    static FailedMessageRetry ToFailedMessageRetry(FailedMessageRetryEntity entity)
    {
        return new FailedMessageRetry
        {
            Id = entity.Id.ToString(),
            FailedMessageId = entity.FailedMessageId,
            RetryBatchId = entity.RetryBatchId,
            StageAttempts = entity.StageAttempts
        };
    }

    static FailedMessage ToFailedMessage(FailedMessageEntity entity)
    {
        // This is a simplified conversion - we'll need to expand this when implementing IErrorMessageDataStore
        var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonOptions) ?? [];
        var failureGroups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(entity.FailureGroupsJson, JsonOptions) ?? [];

        return new FailedMessage
        {
            Id = entity.Id.ToString(),
            UniqueMessageId = entity.UniqueMessageId,
            Status = entity.Status,
            ProcessingAttempts = processingAttempts,
            FailureGroups = failureGroups
        };
    }
}
