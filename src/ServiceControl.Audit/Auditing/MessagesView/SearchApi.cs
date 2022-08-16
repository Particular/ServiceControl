namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    class SearchApi : ApiBase<string, IList<MessagesView>>
    {
        public SearchApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, string input)
        {
            return await DataStore.QueryMessages(request, input).ConfigureAwait(false);
        }
    }
}