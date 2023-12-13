namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class GetAllMessagesApi : ScatterGatherApiMessageView<IErrorMessageDataStore, ScatterGatherApiMessageViewWithSystemMessagesContext>
    {
        public GetAllMessagesApi(IErrorMessageDataStore dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : base(dataStore, settings, httpClientFactory, httpContextAccessor)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewWithSystemMessagesContext input)
        {
            return DataStore.GetAllMessages(input.PagingInfo, input.SortInfo, input.IncludeSystemMessages);
        }
    }
}