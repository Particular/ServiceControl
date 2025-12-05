namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public record SearchApiContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        string SearchQuery,
        DateTimeRange TimeSentRange = null)
        : ScatterGatherApiMessageViewContext(PagingInfo, SortInfo, TimeSentRange);

    public class SearchApi : ScatterGatherApiMessageView<IErrorMessageDataStore, SearchApiContext>
    {
        public SearchApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger<SearchApi> logger)
            : base(dataStore, settings, httpClientFactory, httpContextAccessor, logger)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(SearchApiContext input) =>
            DataStore.GetAllMessagesForSearch(input.SearchQuery, input.PagingInfo, input.SortInfo, input.TimeSentRange);
    }
}