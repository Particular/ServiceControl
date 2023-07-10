namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class SearchEndpointApi : ScatterGatherApiMessageView<SearchEndpointApi.Input>
    {
        public SearchEndpointApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, Input input)
        {
            using (var session = DataStore.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input.Keyword)
                    .Where(m => m.ReceivingEndpointName == input.Endpoint)
                    .Sort(request)
                    .Paging(request)
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }

        public class Input
        {
            public string Keyword { get; set; }
            public string Endpoint { get; set; }
        }
    }
}