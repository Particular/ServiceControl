namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;

    class GetAllMessagesForEndpointApi : ApiBase<string, IList<MessagesView>>
    {
        public override async Task<QueryResult<IList<MessagesView>>> Query(Request request, string input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Where(m => m.ReceivingEndpointName == input)
                    .Statistics(out var stats)
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