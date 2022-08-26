namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.Audit.Persistence;

    class GetAllMessagesForEndpointApi : ApiBase<string, IList<MessagesView>>
    {
        public GetAllMessagesForEndpointApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, string input)
        {
            var pagingInfo = request.GetPagingInfo();
            var sortInfo = request.GetSortInfo();
            return await DataStore.QueryMessagesByReceivingEndpoint(request, input, pagingInfo, sortInfo).ConfigureAwait(false);
        }
    }
}