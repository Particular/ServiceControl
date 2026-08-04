namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class RetryStagingStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : DataStoreBase(scopeFactory), IRetryStagingStore
{
    public Task<RetryBatch?> GetStagingBatch() =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = await dbContext.RetryBatches
                .AsNoTracking()
                .Where(batch => batch.Status == RetryBatchStatus.Staging)
                .OrderBy(batch => batch.StartTime)
                .ThenBy(batch => batch.Id)
                .FirstOrDefaultAsync();

            return batch?.ToRetryBatch(await CountMessages(dbContext, batch.Id));
        });

    public Task<StagingMessage[]> GetMessagesToStage(string batchId) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = ParseBatchId(batchId);

            // A message claimed by an earlier batch is not claimed by this one, and a claim whose
            // message is gone drops out of the join, which is what leaves it out of the staging.
            var rows = await dbContext.FailedMessageRetries
                .AsNoTracking()
                .Where(retry => retry.RetryBatchId == batch)
                .Join(dbContext.FailedMessages.AsNoTracking(),
                    retry => retry.UniqueMessageId,
                    message => message.UniqueMessageId,
                    (retry, message) => new
                    {
                        message.UniqueMessageId,
                        message.MessageId,
                        message.FailingEndpointAddress,
                        message.HeadersJson,
                        retry.StageAttempts
                    })
                .ToListAsync();

            return rows.Select(row => new StagingMessage(
                row.UniqueMessageId.ToString(),
                row.UniqueMessageId.ToString(),
                row.MessageId!,
                row.FailingEndpointAddress,
                MessageHeaders.Read(row.HeadersJson),
                row.StageAttempts)).ToArray();
        });

    public Task MarkBatchAsForwarding(string batchId, string stagingId, IReadOnlyCollection<string> stagedMessageIds) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = ParseBatchId(batchId);
            var staged = ParseMessageIds(stagedMessageIds);
            var now = timeProvider.GetUtcNow().UtcDateTime;

            await InTransaction(dbContext, async () =>
            {
                await dbContext.RetryBatches
                    .Where(row => row.Id == batch)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.Status, RetryBatchStatus.Forwarding)
                        .SetProperty(row => row.StagingId, stagingId));

                // The batch keeps only what it staged, so its message count is what the forwarder is
                // told to expect. The claims dropped here are of messages that no longer exist.
                await dbContext.FailedMessageRetries
                    .Where(row => row.RetryBatchId == batch && !staged.Contains(row.UniqueMessageId))
                    .ExecuteDeleteAsync();

                await dbContext.FailedMessages
                    .Where(row => staged.Contains(row.UniqueMessageId))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.Status, FailedMessageStatus.RetryIssued)
                        .SetProperty(row => row.StatusChangedAt, now)
                        .SetProperty(row => row.LastModified, now));

                await PointForwarderAt(dbContext, batch);
            });
        });

    public Task DiscardBatch(string batchId) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = ParseBatchId(batchId);

            await InTransaction(dbContext, async () =>
            {
                // Nothing was staged, so every claim of this batch is of a message that is gone.
                await dbContext.FailedMessageRetries
                    .Where(row => row.RetryBatchId == batch)
                    .ExecuteDeleteAsync();

                await dbContext.RetryBatches
                    .Where(row => row.Id == batch)
                    .ExecuteDeleteAsync();
            });
        });

    public Task<string?> GetForwardingBatchId() =>
        ExecuteWithDbContext(async dbContext =>
        {
            var nowForwarding = await dbContext.RetryBatchNowForwarding
                .AsNoTracking()
                .SingleOrDefaultAsync();

            return nowForwarding?.RetryBatchId.ToString();
        });

    public Task<RetryBatch?> GetBatch(string batchId, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = ParseBatchId(batchId);

            var entity = await dbContext.RetryBatches
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == batch, cancellationToken);

            return entity?.ToRetryBatch(await CountMessages(dbContext, batch, cancellationToken));
        });

    public Task CompleteForwarding(string batchId) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = ParseBatchId(batchId);

            await InTransaction(dbContext, async () =>
            {
                // The claims outlive the batch: they are what stops a message being staged again
                // before its retry is confirmed.
                await dbContext.RetryBatches
                    .Where(row => row.Id == batch)
                    .ExecuteDeleteAsync();

                await dbContext.RetryBatchNowForwarding
                    .Where(row => row.RetryBatchId == batch)
                    .ExecuteDeleteAsync();
            });
        });

    public Task RecordStagingFailure(IReadOnlyCollection<string> uniqueMessageIds)
    {
        var failed = ParseMessageIds(uniqueMessageIds);

        return ExecuteWithDbContext(dbContext => dbContext.FailedMessageRetries
            .Where(row => failed.Contains(row.UniqueMessageId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.StageAttempts, 1)));
    }

    public Task IncrementStagingAttempts(string uniqueMessageId)
    {
        if (!Guid.TryParse(uniqueMessageId, out var message))
        {
            return Task.CompletedTask;
        }

        return ExecuteWithDbContext(dbContext => dbContext.FailedMessageRetries
            .Where(row => row.UniqueMessageId == message)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.StageAttempts, row => row.StageAttempts + 1)));
    }

    public Task RemoveFromBatch(string uniqueMessageId)
    {
        if (!Guid.TryParse(uniqueMessageId, out var message))
        {
            return Task.CompletedTask;
        }

        return ExecuteWithDbContext(dbContext => dbContext.FailedMessageRetries
            .Where(row => row.UniqueMessageId == message)
            .ExecuteDeleteAsync());
    }

    static async Task PointForwarderAt(ServiceControlDbContext dbContext, Guid batch)
    {
        var updated = await dbContext.RetryBatchNowForwarding
            .Where(row => row.Id == RetryBatchNowForwardingEntity.SingleRowId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.RetryBatchId, batch));

        if (updated == 0)
        {
            dbContext.RetryBatchNowForwarding.Add(new RetryBatchNowForwardingEntity { RetryBatchId = batch });
            await dbContext.SaveChangesAsync();
        }
    }

    static Task<int> CountMessages(ServiceControlDbContext dbContext, Guid batch, CancellationToken cancellationToken = default) =>
        dbContext.FailedMessageRetries
            .AsNoTracking()
            .CountAsync(retry => retry.RetryBatchId == batch, cancellationToken);

    static Task InTransaction(ServiceControlDbContext dbContext, Func<Task> operations) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            await operations();

            await transaction.CommitAsync();
        });

    // Message ids reach this from the API, so an id that is not a message id cannot match a stored
    // message and is left out rather than thrown at.
    static HashSet<Guid> ParseMessageIds(IReadOnlyCollection<string> uniqueMessageIds)
    {
        var parsed = new HashSet<Guid>(uniqueMessageIds.Count);

        foreach (var uniqueMessageId in uniqueMessageIds)
        {
            if (Guid.TryParse(uniqueMessageId, out var message))
            {
                parsed.Add(message);
            }
        }

        return parsed;
    }

    // Batch ids only ever come from CreateBatch, so anything else is a programming error.
    static Guid ParseBatchId(string batchId) =>
        Guid.TryParse(batchId, out var parsed)
            ? parsed
            : throw new ArgumentException($"'{batchId}' is not a retry batch id issued by this store.", nameof(batchId));
}
