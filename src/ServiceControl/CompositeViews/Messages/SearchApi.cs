namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public record SearchApiContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        string SearchQuery)
        : ScatterGatherApiMessageViewContext(PagingInfo, SortInfo);

    public class SearchApi : ScatterGatherApiMessageView<IErrorMessageDataStore, SearchApiContext>
    {
        public SearchApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor) : base(dataStore, settings, httpClientFactory,
            httpContextAccessor)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(SearchApiContext input) =>
            DataStore.GetAllMessagesForSearch(input.SearchQuery, input.PagingInfo, input.SortInfo);
    }
}