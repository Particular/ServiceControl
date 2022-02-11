namespace ServiceControl.Audit.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using Infrastructure;
    using Infrastructure.SQL;
    using Raven.Client;

    class GetKnownEndpointsApi : ApiBaseNoInput<IList<KnownEndpointsView>>
    {
        public GetKnownEndpointsApi(SqlQueryStore queryStore) : base(queryStore)
        {
        }

        protected override async Task<QueryResult<IList<KnownEndpointsView>>> Query(HttpRequestMessage request)
        {
            var result = await Store.GetKnownEndpoints(out var totalCount).ConfigureAwait(false);

            return new QueryResult<IList<KnownEndpointsView>>(result, new QueryStatsInfo(string.Empty, totalCount));
        }
    }
}