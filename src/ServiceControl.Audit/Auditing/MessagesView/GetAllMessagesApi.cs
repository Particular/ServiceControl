namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.Audit.Persistence;

    class GetAllMessagesApi : ApiBaseNoInput<IList<MessagesView>>
    {
        public GetAllMessagesApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request)
        {
            var pagingInfo = request.GetPagingInfo();
            var sortInfo = request.GetSortInfo();
            var includeSystemMessages = request.GetIncludeSystemMessages();
            return await DataStore.GetMessages(includeSystemMessages, pagingInfo, sortInfo).ConfigureAwait(false);
        }
    }
}