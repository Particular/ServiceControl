namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;

/// <summary>
/// Every operation here has to update both StatusChangedAt and LastModified.
/// </summary>
public class FailedMessageLifecycleDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageLifecycleDataStore
{
    public async Task MarkAsArchived(string failedMessageId)
    {
        await ExecuteWithDbContext(async dbContext =>
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
                    .SetProperty(fm => fm.LastModified, now));
        });
    }

    public async Task<bool> MarkAsResolved(string failedMessageId)
    {
        return await ExecuteWithDbContext(async dbContext =>
        {
            if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
            {
                return false;
            }

            var now = DateTime.UtcNow;

            var affected = await dbContext.FailedMessages
                .Where(fm => fm.UniqueMessageId == uniqueMessageId && fm.Status == FailedMessageStatus.Unresolved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Resolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now));

            return affected > 0;
        });
    }

    public async Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds)
    {
        var ids = failedMessageIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
            .Where(guid => guid != Guid.Empty)
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await ExecuteWithDbContext(async dbContext =>
        {
            var now = DateTime.UtcNow;

            // Query which messages will actually be unarchived (must be Archived status)
            var unarchivableIds = await dbContext.FailedMessages
                .Where(fm => ids.Contains(fm.UniqueMessageId) && fm.Status == FailedMessageStatus.Archived)
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync();

            if (unarchivableIds.Count == 0)
            {
                return [];
            }

            await dbContext.FailedMessages
                .Where(fm => unarchivableIds.Contains(fm.UniqueMessageId) && fm.Status == FailedMessageStatus.Archived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now));

            return unarchivableIds.Select(id => id.ToString()).ToArray();
        });
    }

    public async Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to)
    {
        return await ExecuteWithDbContext(async dbContext =>
        {
            var now = DateTime.UtcNow;

            // Query which messages will be unarchived (must be Archived and within the date range)
            var unarchivableIds = await dbContext.FailedMessages
                .Where(fm => fm.Status == FailedMessageStatus.Archived
                    && fm.LastTimeOfFailure >= from
                    && fm.LastTimeOfFailure <= to)
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync();

            if (unarchivableIds.Count == 0)
            {
                return [];
            }

            await dbContext.FailedMessages
                .Where(fm => unarchivableIds.Contains(fm.UniqueMessageId) && fm.Status == FailedMessageStatus.Archived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now));

            return unarchivableIds.Select(id => id.ToString()).ToArray();
        });
    }

    public async Task RevertRetry(string messageUniqueId)
    {
        await ExecuteWithDbContext(async dbContext =>
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
                    .SetProperty(fm => fm.LastModified, now));
        });
    }
}