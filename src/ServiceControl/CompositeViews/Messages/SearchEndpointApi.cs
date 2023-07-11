namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class SearchEndpointApi : ScatterGatherApiMessageView<IErrorMessageDataStore, SearchEndpointApi.Input>
    {
        public SearchEndpointApi(IErrorMessageDataStore dataStore, Settings settings, Func<HttpClient> httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, Input input)
        {
            return DataStore.GetAllMessagesForEndpoint(request.GetPagingInfo(), request.GetSortInfo());
        }

        public class Input
        {
            public string Keyword { get; set; }
            public string Endpoint { get; set; }
        }
    }
}