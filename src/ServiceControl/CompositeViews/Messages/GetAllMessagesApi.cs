namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using ServiceControl.Infrastructure.Extensions;

    public class GetAllMessagesApi : ScatterGatherApiMessageView<NoInput>
    {
        public override async Task<QueryResult<List<MessagesView>>> LocalQuery(Request request, NoInput input, string instanceId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;

                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out stats)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<List<MessagesView>>(results.ToList(), instanceId, stats.ToQueryStatsInfo());
            }
        }
    }
}