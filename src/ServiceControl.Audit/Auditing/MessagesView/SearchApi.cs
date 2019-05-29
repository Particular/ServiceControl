namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;

    class SearchApi : ApiBase<string, IList<MessagesView>>
    {
        public override async Task<QueryResult<IList<MessagesView>>> Query(Request request, string input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }
    }
}