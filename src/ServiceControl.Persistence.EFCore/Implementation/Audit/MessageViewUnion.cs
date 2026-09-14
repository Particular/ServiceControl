namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using Microsoft.EntityFrameworkCore;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.Infrastructure;

/// <summary>
/// One page of failed and audited messages from one statement per view. Each branch carries its
/// own sort and limit, so the database merges two index-ordered scans and stops at the page
/// boundary instead of materialising both tables; paging stays exact because any row of the page
/// is within the top offset + size rows of its own branch.
/// </summary>
static class MessageViewUnion
{
    /// <summary>
    /// An exact total is linear in the audit table, so the count stops here. Total-Count and the
    /// paging links report the cap when it is reached.
    /// </summary>
    public const int TotalCountCap = 100_000;

    /// <param name="audited">Null where this host holds no audit data, which leaves the failed branch alone.</param>
    public static async Task<QueryResult<IList<MessagesView>>> ToPagedMessagesResult(
        IQueryable<MessageRow> failed,
        IQueryable<MessageRow>? audited,
        PagingInfo pagingInfo,
        SortInfo sortInfo,
        CancellationToken cancellationToken = default)
    {
        var reach = pagingInfo.Offset + pagingInfo.Next;

        var page = audited is null
            ? failed.Sort(sortInfo)
            : failed.Sort(sortInfo).Take(reach).Concat(audited.Sort(sortInfo).Take(reach)).Sort(sortInfo);

        var rows = await page
            .Skip(pagingInfo.Offset)
            .Take(pagingInfo.Next)
            .ToListAsync(cancellationToken);

        var counted = failed.Select(row => row.UniqueMessageId);

        if (audited is not null)
        {
            counted = counted.Concat(audited.Select(row => row.UniqueMessageId));
        }

        var total = await counted
            .Take(TotalCountCap)
            .LongCountAsync(cancellationToken);

        IList<MessagesView> results = [.. rows.Select(row => row.ToMessagesView())];

        var version = DataVersion.OverRows(
            [("messages", total)],
            rows,
            row => [row.UniqueMessageId, row.Version, row.Status, row.Revision]);

        return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(version, total));
    }
}
