namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class RetryHistoryDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IRetryHistoryDataStore
{
    public Task<QueryResult<RetryHistory>> GetRetryHistory(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var historicOperations = await dbContext.HistoricRetryOperations
                .AsNoTracking()
                .OrderByDescending(operation => operation.CompletionTime)
                .ThenByDescending(operation => operation.Id)
                .Select(operation => new HistoricRetryOperation
                {
                    RequestId = operation.RequestId,
                    RetryType = operation.RetryType,
                    StartTime = operation.StartTime,
                    CompletionTime = operation.CompletionTime,
                    Originator = operation.Originator,
                    Failed = operation.Failed,
                    NumberOfMessagesProcessed = operation.NumberOfMessagesProcessed
                })
                .ToListAsync(token);

            var unacknowledgedOperations = await dbContext.UnacknowledgedRetryOperations
                .AsNoTracking()
                .Select(operation => new UnacknowledgedRetryOperation
                {
                    RequestId = operation.RequestId,
                    RetryType = operation.RetryType,
                    StartTime = operation.StartTime,
                    CompletionTime = operation.CompletionTime,
                    Last = operation.Last,
                    Originator = operation.Originator,
                    Classifier = operation.Classifier,
                    Failed = operation.Failed,
                    NumberOfMessagesProcessed = operation.NumberOfMessagesProcessed
                })
                .ToListAsync(token);

            var history = new RetryHistory
            {
                HistoricOperations = historicOperations,
                UnacknowledgedOperations = unacknowledgedOperations
            };

            return new QueryResult<RetryHistory>(history, history.ToQueryStatsInfo());
        }, cancellationToken);

    public Task RecordRetryOperationCompleted(string requestId, RetryType retryType, DateTime startTime, DateTime completionTime,
        string? originator, string? classifier, bool messageFailed, int numberOfMessagesProcessed, DateTime lastProcessed, int retryHistoryDepth,
        CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(token);

                dbContext.HistoricRetryOperations.Add(new HistoricRetryOperationEntity
                {
                    RequestId = requestId,
                    RetryType = retryType,
                    StartTime = startTime,
                    CompletionTime = completionTime,
                    Originator = originator,
                    Failed = messageFailed,
                    NumberOfMessagesProcessed = numberOfMessagesProcessed
                });

                if (NeedsAcknowledgement(retryType))
                {
                    await RecordUnacknowledged(dbContext, requestId, retryType, startTime, completionTime,
                        originator, classifier, messageFailed, numberOfMessagesProcessed, lastProcessed, token);
                }

                await dbContext.SaveChangesAsync(token);

                // After the insert, so the operation just recorded competes for a place in the history.
                await TrimHistory(dbContext, retryHistoryDepth, token);

                await transaction.CommitAsync(token);
            });
        }, cancellationToken);

    public Task<bool> AcknowledgeRetryGroup(string groupId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var acknowledged = await dbContext.UnacknowledgedRetryOperations
                .Where(operation => operation.RequestId == groupId && operation.RetryType == RetryType.FailureGroup)
                .ExecuteDeleteAsync(token);

            return acknowledged > 0;
        }, cancellationToken);

    static bool NeedsAcknowledgement(RetryType retryType) =>
        retryType is not RetryType.SingleMessage and not RetryType.MultipleMessages;

    static async Task RecordUnacknowledged(ServiceControlDbContext dbContext, string requestId, RetryType retryType,
        DateTime startTime, DateTime completionTime, string? originator, string? classifier, bool messageFailed,
        int numberOfMessagesProcessed, DateTime lastProcessed, CancellationToken cancellationToken)
    {
        var unacknowledged = await dbContext.UnacknowledgedRetryOperations
            .SingleOrDefaultAsync(operation => operation.RequestId == requestId && operation.RetryType == retryType, cancellationToken);

        if (unacknowledged == null)
        {
            unacknowledged = new UnacknowledgedRetryOperationEntity { RequestId = requestId, RetryType = retryType };
            dbContext.UnacknowledgedRetryOperations.Add(unacknowledged);
        }

        unacknowledged.StartTime = startTime;
        unacknowledged.CompletionTime = completionTime;
        unacknowledged.Last = lastProcessed;
        unacknowledged.Originator = originator;
        unacknowledged.Classifier = classifier;
        unacknowledged.Failed = messageFailed;
        unacknowledged.NumberOfMessagesProcessed = numberOfMessagesProcessed;
    }

    static async Task TrimHistory(ServiceControlDbContext dbContext, int retryHistoryDepth, CancellationToken cancellationToken)
    {
        if (retryHistoryDepth <= 0)
        {
            await dbContext.HistoricRetryOperations.ExecuteDeleteAsync(cancellationToken);
            return;
        }

        // The oldest operation worth keeping. Everything ordering below it is over the depth.
        var cutoff = await dbContext.HistoricRetryOperations
            .AsNoTracking()
            .OrderByDescending(operation => operation.CompletionTime)
            .ThenByDescending(operation => operation.Id)
            .Skip(retryHistoryDepth - 1)
            .Select(operation => new { operation.CompletionTime, operation.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (cutoff == null)
        {
            return;
        }

        await dbContext.HistoricRetryOperations
            .Where(operation => operation.CompletionTime < cutoff.CompletionTime
                || (operation.CompletionTime == cutoff.CompletionTime && operation.Id < cutoff.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
