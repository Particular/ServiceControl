namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;

public class RetryBatchesDataStore : DataStoreBase, IRetryBatchesDataStore
{
    readonly ILogger<RetryBatchesDataStore> logger;

    public RetryBatchesDataStore(
        IServiceScopeFactory scopeFactory,
        ILogger<RetryBatchesDataStore> logger) : base(scopeFactory)
    {
        this.logger = logger;
    }

    public Task<IRetryBatchesManager> CreateRetryBatchesManager()
    {
        var scope = CreateScope();
        return Task.FromResult<IRetryBatchesManager>(
            new RetryBatchesManager(scope, logger));
    }

    public Task RecordFailedStagingAttempt(
        IReadOnlyCollection<FailedMessage> messages,
        IReadOnlyDictionary<string, FailedMessageRetry> failedMessageRetriesById,
        Exception e,
        int maxStagingAttempts,
        string stagingId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            foreach (var failedMessage in messages)
            {
                var failedMessageRetry = failedMessageRetriesById[failedMessage.Id];

                logger.LogWarning(e, "Attempt 1 of {MaxStagingAttempts} to stage a retry message {UniqueMessageId} failed",
                    maxStagingAttempts, failedMessage.UniqueMessageId);

                var entity = await dbContext.FailedMessageRetries
                    .FirstOrDefaultAsync(f => f.Id == Guid.Parse(failedMessageRetry.Id));

                if (entity != null)
                {
                    entity.StageAttempts = 1;
                }
            }

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while incrementing staging attempt count for {StagingId}",
                    stagingId);
            }
        });
    }

    public Task IncrementAttemptCounter(FailedMessageRetry message)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            try
            {
                await dbContext.FailedMessageRetries
                    .Where(f => f.Id == Guid.Parse(message.Id))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.StageAttempts, f => f.StageAttempts + 1));
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while incrementing staging attempt count for {MessageId}",
                    message.FailedMessageId);
            }
        });
    }

    public Task DeleteFailedMessageRetry(string uniqueMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var documentId = FailedMessageRetry.MakeDocumentId(uniqueMessageId);

            await dbContext.FailedMessageRetries
                .Where(f => f.Id == Guid.Parse(documentId))
                .ExecuteDeleteAsync();
        });
    }
}
