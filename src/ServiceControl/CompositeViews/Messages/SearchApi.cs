namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
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

    public class SearchApi : ScatterGatherApiMessageView<IMessagesViewDataStore, SearchApiContext>
    {
        public SearchApi(IMessagesViewDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger<SearchApi> logger)
            : base(dataStore, settings, httpClientFactory, httpContextAccessor, logger)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(SearchApiContext input, CancellationToken cancellationToken = default) =>
            DataStore.GetAllMessagesForSearch(input.SearchQuery, input.PagingInfo, input.SortInfo, input.TimeSentRange);
    }
}