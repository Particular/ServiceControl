namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    public class GetAuditCountsForEndpointApi : ApiBase<string, IList<AuditCount>>
    {
        public GetAuditCountsForEndpointApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<AuditCount>>> Query(HttpRequestMessage request, string endpointName)
        {
            return await DataStore.QueryAuditCounts(endpointName);
        }
    }
}