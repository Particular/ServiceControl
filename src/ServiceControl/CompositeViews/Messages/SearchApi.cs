namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class SearchApi : ScatterGatherApiMessageView<IErrorMessageDataStore, string>
    {
        public SearchApi(IErrorMessageDataStore dataStore, Settings settings, Func<HttpClient> httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, string input)
        {
            // TODO: Will the INPUT format be identical between RavenDB 3.x and 5.x? Also, what if we want to support a different storage engine?
            var pagingInfo = request.GetPagingInfo();
            var sortInfo = request.GetSortInfo();
            return DataStore.GetAllMessagesForSearch(input, pagingInfo, sortInfo);
        }
    }
}