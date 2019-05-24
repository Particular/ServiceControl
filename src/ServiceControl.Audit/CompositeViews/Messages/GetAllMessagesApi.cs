namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;

    class GetAllMessagesApi : ApiBase<NoInput, List<MessagesView>>
    {
        public override async Task<QueryResult<List<MessagesView>>> Query(Request request, NoInput input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<List<MessagesView>>(results.ToList(), stats.ToQueryStatsInfo());
            }
        }
    }
}