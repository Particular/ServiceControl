namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.SQL;

    class SearchEndpointApi : ApiBase<SearchEndpointApi.Input, IList<MessagesView>>
    {
        public SearchEndpointApi(SqlQueryStore queryStore) : base(queryStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, Input input)
        {
            var results = await Store.SearchEndpoint(request, input, out var stats).ConfigureAwait(false);

            return new QueryResult<IList<MessagesView>>(results, stats);
        }

        public class Input
        {
            public string Keyword { get; set; }
            public string Endpoint { get; set; }
        }
    }
}