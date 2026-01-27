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

    public record AllMessagesForEndpointContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        bool IncludeSystemMessages,
        string EndpointName,
        DateTimeRange TimeSentRange = null)
        : ScatterGatherApiMessageViewWithSystemMessagesContext(PagingInfo, SortInfo, IncludeSystemMessages, TimeSentRange);

    public class GetAllMessagesForEndpointApi : ScatterGatherApiMessageView<IErrorMessageDataStore, AllMessagesForEndpointContext>
    {
        public GetAllMessagesForEndpointApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, ILogger<GetAllMessagesForEndpointApi> logger)
            : base(dataStore, settings, httpClientFactory, httpContextAccessor, logger)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(AllMessagesForEndpointContext input) => DataStore.GetAllMessagesForEndpoint(input.EndpointName, input.PagingInfo, input.SortInfo, input.IncludeSystemMessages, input.TimeSentRange);
    }
}