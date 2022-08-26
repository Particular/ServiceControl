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
            var includeSystemMessages = request.GetIncludeSystemMessages();
            return await DataStore.QueryMessagesByReceivingEndpoint(includeSystemMessages, input, pagingInfo, sortInfo).ConfigureAwait(false);
        }
    }
}