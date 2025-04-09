namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public record SearchEndpointContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        string Keyword,
        string Endpoint,
        DateTimeRange TimeSentRange = null)
        : ScatterGatherApiMessageViewContext(PagingInfo, SortInfo, TimeSentRange);

    public class SearchEndpointApi : ScatterGatherApiMessageView<IErrorMessageDataStore, SearchEndpointContext>
    {
        public SearchEndpointApi(IErrorMessageDataStore dataStore, Settings settings,
            IHttpClientFactory httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(SearchEndpointContext input) =>
            DataStore.SearchEndpointMessages(input.Endpoint, input.Keyword, input.PagingInfo, input.SortInfo, input.TimeSentRange);
    }
}