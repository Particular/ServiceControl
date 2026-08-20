namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

static class FailedMessageQueryResults
{
    public static async Task<QueryResult<IList<FailedMessageView>>> ToPagedResult(this IQueryable<FailedMessageEntity> source, PagingInfo pagingInfo, SortInfo sortInfo, (string Name, object? Value)[] filters, CancellationToken cancellationToken = default)
    {
        var total = await source.LongCountAsync(cancellationToken);

        var entities = await source
            .Sort(sortInfo)
            .Page(pagingInfo)
            .ToListAsync(cancellationToken);

        IList<FailedMessageView> results = [.. entities.Select(entity => entity.ToFailedMessageView())];

        return new QueryResult<IList<FailedMessageView>>(results, entities.ToPagedQueryStatsInfo(total, QueryNarrowing.Terms(pagingInfo, sortInfo, filters)));
    }
}
