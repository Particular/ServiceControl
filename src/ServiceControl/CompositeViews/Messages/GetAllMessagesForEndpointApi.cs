namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public record AllMessagesForEndpointContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        bool IncludeSystemMessages,
        string EndpointName)
        : ScatterGatherApiMessageViewWithSystemMessagesContext(PagingInfo, SortInfo, IncludeSystemMessages);

    public class GetAllMessagesForEndpointApi : ScatterGatherApiMessageView<IErrorMessageDataStore, AllMessagesForEndpointContext>
    {
        public GetAllMessagesForEndpointApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(AllMessagesForEndpointContext input) => DataStore.GetAllMessagesForEndpoint(input.EndpointName, input.PagingInfo, input.SortInfo, input.IncludeSystemMessages);
    }
}