namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Raven.Client.Documents;
    using ServiceControl.Infrastructure.Extensions;

    class SearchEndpointApi : ApiBase<SearchEndpointApi.Input, IList<MessagesView>>
    {
        public SearchEndpointApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, Input input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .Search(x => x.Query, input.Keyword)
                    .Where(m => m.ReceivingEndpointName == input.Endpoint, false)
                    .Sort(request)
                    .Paging(request)
                    .ToMessagesView()
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