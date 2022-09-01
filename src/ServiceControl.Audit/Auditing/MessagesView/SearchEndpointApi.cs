namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence;

    class SearchEndpointApi : ApiBase<SearchEndpointApi.Input, IList<MessagesView>>
    {
        public SearchEndpointApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, Input input)
        {
            var pagingInfo = request.GetPagingInfo();
            var sortInfo = request.GetSortInfo();
            return await DataStore.QueryMessagesByReceivingEndpointAndKeyword(input.Endpoint, input.Keyword, pagingInfo, sortInfo).ConfigureAwait(false);
        }

        public class Input
        {
            public string Keyword { get; set; }
            public string Endpoint { get; set; }
        }
    }
}