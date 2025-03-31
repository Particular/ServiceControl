namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public class GetAllMessagesApi : ScatterGatherApiMessageView<IErrorMessageDataStore, ScatterGatherApiMessageViewWithSystemMessagesContext>
    {
        public GetAllMessagesApi(IErrorMessageDataStore dataStore, Settings settings,
            IHttpClientFactory httpClientFactory) : base(dataStore, settings, httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewWithSystemMessagesContext input)
        {
            return DataStore.GetAllMessages(input.PagingInfo, input.SortInfo, input.IncludeSystemMessages, input.TimeSentRange);
        }
    }
}