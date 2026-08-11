namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

static class FailedMessageQueryResults
{
    public static async Task<QueryResult<IList<FailedMessageView>>> ToPagedResult(this IQueryable<FailedMessageEntity> source, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default)
    {
        var stats = await source.ToQueryStatsInfo(cancellationToken);

        var entities = await source
            .Sort(sortInfo)
            .Page(pagingInfo)
            .ToListAsync(cancellationToken);

        IList<FailedMessageView> results = [.. entities.Select(entity => entity.ToFailedMessageView())];

        return new QueryResult<IList<FailedMessageView>>(results, stats);
    }
}
