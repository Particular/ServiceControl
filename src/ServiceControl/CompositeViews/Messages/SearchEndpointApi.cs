namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public record SearchEndpointContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        string Keyword,
        string Endpoint)
        : ScatterGatherApiMessageViewContext(PagingInfo, SortInfo);

    public class SearchEndpointApi : ScatterGatherApiMessageView<IErrorMessageDataStore, SearchEndpointContext>
    {
        public SearchEndpointApi(IErrorMessageDataStore dataStore, Settings settings,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor) : base(dataStore, settings, httpClientFactory,
            httpContextAccessor)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(SearchEndpointContext input) =>
            DataStore.SearchEndpointMessages(input.Endpoint, input.Keyword, input.PagingInfo, input.SortInfo);
    }
}