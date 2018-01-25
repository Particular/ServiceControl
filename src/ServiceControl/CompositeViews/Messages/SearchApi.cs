namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using ServiceControl.Infrastructure.Extensions;

    public class SearchApi : ScatterGatherApiMessageView<string>
    {
        public override async Task<QueryResult<List<MessagesView>>> LocalQuery(Request request, string input, string instanceId)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;

                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out stats)
                    .Search(x => x.Query, input)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Results(results.ToList(), stats);
            }
        }
    }
}