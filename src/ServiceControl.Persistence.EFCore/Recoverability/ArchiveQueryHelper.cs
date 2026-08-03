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
    /// <summary>
    /// Returns the count of unresolved messages in a group and the group's title.
    /// Used when starting an archive operation.
    /// </summary>
    public static async Task<(int count, string groupName)> GetGroupDetailsForArchive(
        ServiceControlDbContext dbContext, string groupId, CancellationToken cancellationToken = default)
    {
        return await GetGroupDetails(dbContext, groupId, FailedMessageStatus.Unresolved, cancellationToken);
    }

    /// <summary>
    /// Returns the count of archived messages in a group and the group's title.
    /// Used when starting an unarchive operation.
    /// </summary>
    public static async Task<(int count, string groupName)> GetGroupDetailsForUnarchive(
        ServiceControlDbContext dbContext, string groupId, CancellationToken cancellationToken = default)
    {
        return await GetGroupDetails(dbContext, groupId, FailedMessageStatus.Archived, cancellationToken);
    }

    static async Task<(int count, string groupName)> GetGroupDetails(
        ServiceControlDbContext dbContext, string groupId, FailedMessageStatus status, CancellationToken cancellationToken)
    {
        var query = from fmg in dbContext.FailedMessageGroups
                    join fm in dbContext.FailedMessages on fmg.FailedMessageUniqueId equals fm.UniqueMessageId
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
    /// Keyset-paginated query: selects the next batch of message IDs in a group
    /// with the given status, ordered by UniqueMessageId, starting after lastProcessedId.
    /// </summary>
    public static async Task<List<Guid>> GetNextBatchOfMessageIds(
        ServiceControlDbContext dbContext,
        string groupId,
        FailedMessageStatus status,
        Guid lastProcessedId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var query = from fmg in dbContext.FailedMessageGroups
                    join fm in dbContext.FailedMessages on fmg.FailedMessageUniqueId equals fm.UniqueMessageId
                    where fmg.GroupId == groupId
                          && fm.Status == status
                          && fm.UniqueMessageId > lastProcessedId
                    orderby fm.UniqueMessageId
                    select fm.UniqueMessageId;

        return await query.Take(batchSize).ToListAsync(cancellationToken);
    }
}