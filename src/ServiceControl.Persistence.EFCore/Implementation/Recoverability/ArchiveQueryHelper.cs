namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// Focused query helpers for the archive/unarchive flows: group details (count + name)
/// and keyset-paginated batch selection of message IDs by group + status.
/// </summary>
static class ArchiveQueryHelper
{
    internal static async Task<(int count, string groupName)> GetGroupDetails(
        ServiceControlDbContext dbContext, string groupId, FailedMessageStatus status, CancellationToken cancellationToken = default)
    {
        var query =
            from fmg in dbContext.FailedMessageGroups
            join fm in dbContext.FailedMessages
                on fmg.FailedMessageUniqueId equals fm.UniqueMessageId
            where fmg.GroupId == groupId && fm.Status == status
            select new { fmg.Title, fm.UniqueMessageId };

        var count = await query.CountAsync(cancellationToken);
        var groupName = await dbContext.FailedMessageGroups
            .Where(fmg => fmg.GroupId == groupId)
            .Select(fmg => fmg.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? "Undefined";

        return (count, groupName);
    }

    /// <summary>
    /// Selects the next batch of message IDs in a group with the given status, ordered by
    /// UniqueMessageId. No cursor is needed — once a batch is archived (or unarchived), the
    /// status change excludes those messages from the next query, so each call naturally
    /// returns the next unprocessed batch.
    /// </summary>
    public static async Task<List<Guid>> GetNextBatchOfMessageIds(
        ServiceControlDbContext dbContext,
        string groupId,
        FailedMessageStatus status,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var query = from fmg in dbContext.FailedMessageGroups
                    join fm in dbContext.FailedMessages on fmg.FailedMessageUniqueId equals fm.UniqueMessageId
                    where fmg.GroupId == groupId
                          && fm.Status == status
                    orderby fm.UniqueMessageId
                    select fm.UniqueMessageId;

        return await query.Take(batchSize).ToListAsync(cancellationToken);
    }
}