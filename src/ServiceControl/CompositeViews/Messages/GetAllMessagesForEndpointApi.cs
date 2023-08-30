namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class GetAllMessagesForEndpointApi : ScatterGatherApiMessageView<IErrorMessageDataStore, string>
    {
        public GetAllMessagesForEndpointApi(IErrorMessageDataStore dataStore, Settings settings, Func<HttpClient> httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, string endpointName)
        {
            var pagingInfo = request.GetPagingInfo();
            var sortInfo = request.GetSortInfo();
            var includeSystemMessages = request.GetIncludeSystemMessages();

            return DataStore.GetAllMessagesForEndpoint(endpointName, pagingInfo, sortInfo, includeSystemMessages);
        }
    }
}