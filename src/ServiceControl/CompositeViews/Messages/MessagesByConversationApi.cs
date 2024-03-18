namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Persistence;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    public record MessagesByConversationContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        bool IncludeSystemMessages,
        string ConversationId)
        : ScatterGatherApiMessageViewWithSystemMessagesContext(PagingInfo, SortInfo, IncludeSystemMessages);

    public class MessagesByConversationApi : ScatterGatherApiMessageView<IErrorMessageDataStore, MessagesByConversationContext>
    {
        public MessagesByConversationApi(IErrorMessageDataStore dataStore, Settings settings,
            IHttpClientFactory httpClientFactory) : base(dataStore, settings,
            httpClientFactory)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(MessagesByConversationContext input) =>
            DataStore.GetAllMessagesByConversation(input.ConversationId, input.PagingInfo, input.SortInfo,
                input.IncludeSystemMessages);
    }
}