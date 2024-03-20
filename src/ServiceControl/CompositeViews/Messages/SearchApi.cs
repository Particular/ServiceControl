namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public record SearchApiContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        string SearchQuery)
        : ScatterGatherApiMessageViewContext(PagingInfo, SortInfo);

    public class SearchApi : ScatterGatherApiMessageView<IErrorMessageDataStore, SearchApiContext>
    {
        public SearchApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory) :
            base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(SearchApiContext input) =>
            DataStore.GetAllMessagesForSearch(input.SearchQuery, input.PagingInfo, input.SortInfo);
    }
}