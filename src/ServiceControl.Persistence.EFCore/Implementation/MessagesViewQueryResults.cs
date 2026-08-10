namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

static class MessagesViewQueryResults
{
    public static async Task<QueryResult<IList<MessagesView>>> ToPagedMessagesResult(this IQueryable<FailedMessageEntity> source, PagingInfo pagingInfo, SortInfo sortInfo)
    {
        var stats = await source.ToQueryStatsInfo();

        var entities = await source
            .SortMessages(sortInfo)
            .Page(pagingInfo)
            .ToListAsync();

        IList<MessagesView> results = [.. entities.Select(entity => entity.ToMessagesView())];

        return new QueryResult<IList<MessagesView>>(results, stats);
    }
}
