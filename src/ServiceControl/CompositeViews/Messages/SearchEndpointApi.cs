namespace ServiceControl.CompositeViews.Messages
{
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.Infrastructure.Extensions;

    public class SearchEndpointApi : ScatterGatherApiMessageView<SearchEndpointApi.Input>
    {

        public class Input
        {
            public string Keyword { get; set; }
            public string Endpoint { get; set; }
        }

        public override async Task<QueryResult<MessagesView>> LocalQuery(Request request, Input input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                RavenQueryStatistics stats;
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out stats)
                    .Search(x => x.Query, input.Keyword)
                    .Where(m => m.ReceivingEndpointName == input.Endpoint)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return Results(results, stats);
            }
        }
    }
}