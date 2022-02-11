namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.SQL;

    class SearchApi : ApiBase<string, IList<MessagesView>>
    {
        public SearchApi(SqlQueryStore queryStore) : base(queryStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, string input)
        {
            var results = await Store.FullTextSearch(request, out var stats).ConfigureAwait(false);

            return new QueryResult<IList<MessagesView>>(results, stats);
        }
    }
}