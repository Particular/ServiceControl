namespace ServiceControl.Audit.Monitoring
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Auditing.MessagesView;
    using ServiceControl.Audit.Persistence;

    class GetKnownEndpointsApi : ApiBaseNoInput<IList<KnownEndpointsView>>
    {
        public GetKnownEndpointsApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<KnownEndpointsView>>> Query(HttpRequestMessage request)
        {
            return await DataStore.QueryKnownEndpoints(request).ConfigureAwait(false);
        }
    }
}