namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;

/// <summary>
/// Every operation here has to update both StatusChangedAt and LastModified.
/// </summary>
public class FailedMessageLifecycleDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageLifecycleDataStore
{
    public async Task MarkAsArchived(string failedMessageId, CancellationToken cancellationToken = default)
    {
        await ExecuteWithDbContext(async (dbContext, token) =>
        {
            if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
            {
                return;
            }

            var now = DateTime.UtcNow;

            await dbContext.FailedMessages
                .Where(fm => fm.UniqueMessageId == uniqueMessageId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Archived)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now), token);
        }, cancellationToken);
    }

    public async Task<bool> MarkAsResolved(string failedMessageId, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithDbContext(async (dbContext, token) =>
        {
            if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
            {
                return false;
            }

            var now = DateTime.UtcNow;

            var affected = await dbContext.FailedMessages
                .Where(fm => fm.UniqueMessageId == uniqueMessageId && fm.Status != FailedMessageStatus.Resolved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Resolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now), token);

            return affected > 0;
        }, cancellationToken);
    }

    public async Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds, CancellationToken cancellationToken = default)
    {
        var ids = failedMessageIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
            .Where(guid => guid != Guid.Empty)
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await ExecuteWithDbContext(async (dbContext, token) =>
        {
            var now = DateTime.UtcNow;

            // Query which messages will actually be unarchived (must be Archived status)
            var unarchivableIds = await dbContext.FailedMessages
                .Where(fm => ids.Contains(fm.UniqueMessageId) && fm.Status == FailedMessageStatus.Archived)
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync(token);

            if (unarchivableIds.Count == 0)
            {
                return [];
            }

            await dbContext.FailedMessages
                .Where(fm => unarchivableIds.Contains(fm.UniqueMessageId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now), token);

            return unarchivableIds.Select(id => id.ToString()).ToArray();
        }, cancellationToken);
    }

    public async Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithDbContext(async (dbContext, token) =>
        {
            var now = DateTime.UtcNow;

            // Query which messages will be unarchived (must be Archived and within the date range)
            var unarchivableIds = await dbContext.FailedMessages
                .Where(fm => fm.Status == FailedMessageStatus.Archived
                    && fm.LastModified >= from
                    && fm.LastModified <= to)
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync(token);

            if (unarchivableIds.Count == 0)
            {
                return [];
            }

            await dbContext.FailedMessages
                .Where(fm => unarchivableIds.Contains(fm.UniqueMessageId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now), token);

            return unarchivableIds.Select(id => id.ToString()).ToArray();
        }, cancellationToken);
    }

    public async Task RevertRetry(string messageUniqueId, CancellationToken cancellationToken = default)
    {
        await ExecuteWithDbContext(async (dbContext, token) =>
        {
            if (!Guid.TryParse(messageUniqueId, out var uniqueMessageId))
            {
                return;
            }

            var now = DateTime.UtcNow;

            await dbContext.FailedMessages
                .Where(fm => fm.UniqueMessageId == uniqueMessageId && fm.Status == FailedMessageStatus.RetryIssued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now), token);
        }, cancellationToken);
    }
}