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
            return await DataStore.GetMessages(request, pagingInfo, sortInfo).ConfigureAwait(false);
        }
    }
}