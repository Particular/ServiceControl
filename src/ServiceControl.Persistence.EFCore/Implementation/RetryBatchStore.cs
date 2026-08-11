namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

public class RetryBatchStore(IServiceScopeFactory scopeFactory, IRetryBatchSqlDialect dialect) : DataStoreBase(scopeFactory), IRetryBatchStore
{
    public Task<string> CreateBatch(string retrySessionId, string requestId, RetryType retryType,
        string[] failedMessageRetryIds, string originator, DateTime startTime, DateTime? last = null,
        string? batchName = null, string? classifier = null,
        string? initiatedById = null, string? initiatedByName = null, string? operationId = null,
        CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = new RetryBatchEntity
            {
                Id = Guid.NewGuid(),
                Status = RetryBatchStatus.MarkingDocuments,
                RetrySessionId = retrySessionId,
                RequestId = requestId,
                RetryType = retryType,
                InitialBatchSize = failedMessageRetryIds.Length,
                StartTime = startTime,
                Last = last,
                Context = batchName,
                Originator = originator,
                Classifier = classifier,
                InitiatedById = initiatedById,
                InitiatedByName = initiatedByName,
                OperationId = operationId
            };

            dbContext.RetryBatches.Add(batch);

            await dbContext.SaveChangesAsync(cancellationToken);

            return batch.Id.ToString();
        });

    /// <summary>
    /// Claims the messages for the batch. A message already claimed by another batch keeps that claim, so the batch it is staged with is whichever one got there first.
    /// </summary>
    public Task AssignMessagesToBatch(string batchId, string[] messageIds, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var batch = ParseBatchId(batchId);

            // Message ids reach this from the API, so an id that is not a message id is a caller's
            // typo rather than a fault. It cannot match a stored message, so it is left unclaimed.
            var uniqueMessageIds = new HashSet<Guid>();

            foreach (var messageId in messageIds)
            {
                if (Guid.TryParse(messageId, out var uniqueMessageId))
                {
                    uniqueMessageIds.Add(uniqueMessageId);
                }
            }

            var claims = uniqueMessageIds
                .Select(uniqueMessageId => new FailedMessageRetryEntity { UniqueMessageId = uniqueMessageId, RetryBatchId = batch })
                .ToArray();

            if (claims.Length == 0)
            {
                return;
            }

            // The dialect writes on the connection directly, so it needs a transaction of its own.
            var strategy = dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                await dialect.InsertMissingRetryClaims(dbContext, claims, CancellationToken.None);

                await transaction.CommitAsync(cancellationToken);
            });
        });

    public Task MoveBatchToStaging(string batchId, CancellationToken cancellationToken = default)
    {
        var batch = ParseBatchId(batchId);

        return ExecuteWithDbContext(dbContext => dbContext.RetryBatches
            .Where(row => row.Id == batch)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, RetryBatchStatus.Staging), cancellationToken));
    }

    // Batch ids only ever come from CreateBatch, so anything else is a programming error.
    static Guid ParseBatchId(string batchId) =>
        Guid.TryParse(batchId, out var parsed)
            ? parsed
            : throw new ArgumentException($"'{batchId}' is not a retry batch id issued by this store.", nameof(batchId));

    public Task<QueryResult<IList<RetryBatch>>> GetOrphanedBatches(string retrySessionId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var orphaned = await dbContext.RetryBatches
                .AsNoTracking()
                .Where(batch => batch.Status == RetryBatchStatus.MarkingDocuments && batch.RetrySessionId != retrySessionId)
                .ToListAsync(cancellationToken);

            var messageCounts = await CountMessages(dbContext, [.. orphaned.Select(batch => batch.Id)], cancellationToken);

            IList<RetryBatch> batches = [.. orphaned.Select(batch => batch.ToRetryBatch(messageCounts.GetValueOrDefault(batch.Id)))];

            return new QueryResult<IList<RetryBatch>>(batches, new QueryStatsInfo(string.Empty, batches.Count, false));
        });

    public Task<IList<RetryBatchGroup>> GetAvailableBatchGroups(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext<IList<RetryBatchGroup>>(async dbContext =>
        {
            var groups = await dbContext.RetryBatches
                .AsNoTracking()
                .Where(batch => batch.Status == RetryBatchStatus.Staging || batch.Status == RetryBatchStatus.Forwarding)
                .GroupBy(batch => new { batch.RequestId, batch.RetryType })
                .Select(group => new
                {
                    group.Key.RequestId,
                    group.Key.RetryType,
                    HasStagingBatches = group.Any(batch => batch.Status == RetryBatchStatus.Staging),
                    HasForwardingBatches = group.Any(batch => batch.Status == RetryBatchStatus.Forwarding),
                    InitialBatchSize = group.Sum(batch => batch.InitialBatchSize),
                    StartTime = group.Min(batch => batch.StartTime),
                    Last = group.Max(batch => batch.Last),
                    Originator = group.Max(batch => batch.Originator),
                    Classifier = group.Max(batch => batch.Classifier)
                })
                .ToListAsync(cancellationToken);

            return [.. groups.Select(group => new RetryBatchGroup
            {
                RequestId = group.RequestId,
                RetryType = group.RetryType,
                HasStagingBatches = group.HasStagingBatches,
                HasForwardingBatches = group.HasForwardingBatches,
                InitialBatchSize = group.InitialBatchSize,
                StartTime = group.StartTime,
                Last = group.Last ?? default,
                Originator = group.Originator,
                Classifier = group.Classifier
            })];
        });

    public Task<ForwardingRetryBatch?> GetCurrentForwardingBatch(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var nowForwarding = await dbContext.RetryBatchNowForwarding
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (nowForwarding == null)
            {
                return null;
            }

            return await dbContext.RetryBatches
                .AsNoTracking()
                .Where(batch => batch.Id == nowForwarding.RetryBatchId)
                .Select(batch => new ForwardingRetryBatch(batch.RequestId, batch.RetryType, batch.Originator!, batch.Classifier!))
                .SingleOrDefaultAsync(cancellationToken);
        });

    public Task ForEachUnresolvedMessage(Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default) =>
        ForEach(Unresolved, callback, cancellationToken);

    public Task ForEachUnresolvedMessageForEndpoint(string endpoint, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default) =>
        ForEach(dbContext => Unresolved(dbContext)
            .Where(message => message.ReceivingEndpointName == endpoint), callback, cancellationToken);

    public Task ForEachMessageForQueueAddress(string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default) =>
        ForEach(dbContext => Unresolved(dbContext)
            .Where(message => message.FailingEndpointAddress == failedQueueAddress && message.Status == status), callback, cancellationToken);

    public Task ForEachUnresolvedMessageInGroup(string groupId, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken = default) =>
        ForEach(dbContext => Unresolved(dbContext)
            .Where(message => dbContext.FailedMessageGroups.Any(group => group.GroupId == groupId && group.FailedMessageUniqueId == message.UniqueMessageId)), callback, cancellationToken);

    Task ForEach(Func<ServiceControlDbContext, IQueryable<FailedMessageEntity>> query, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(dbContext => Stream(query(dbContext), callback, cancellationToken));

    static IQueryable<FailedMessageEntity> Unresolved(ServiceControlDbContext dbContext) =>
        dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => message.Status == FailedMessageStatus.Unresolved);

    static async Task Stream(IQueryable<FailedMessageEntity> messages, Func<string, DateTime, CancellationToken, Task> callback, CancellationToken cancellationToken)
    {
        var rows = messages
            .Select(message => new { message.UniqueMessageId, message.LastTimeOfFailure })
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await callback(row.UniqueMessageId.ToString(), row.LastTimeOfFailure, cancellationToken);
        }
    }

    static async Task<Dictionary<Guid, int>> CountMessages(ServiceControlDbContext dbContext, Guid[] batchIds, CancellationToken cancellationToken)
    {
        if (batchIds.Length == 0)
        {
            return [];
        }

        return await dbContext.FailedMessageRetries
            .AsNoTracking()
            .Where(retry => batchIds.Contains(retry.RetryBatchId))
            .GroupBy(retry => retry.RetryBatchId)
            .Select(group => new { RetryBatchId = group.Key, MessageCount = group.Count() })
            .ToDictionaryAsync(row => row.RetryBatchId, row => row.MessageCount, cancellationToken);
    }
}
