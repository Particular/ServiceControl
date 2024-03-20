namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    public record MessagesByConversationContext(
        PagingInfo PagingInfo,
        SortInfo SortInfo,
        bool IncludeSystemMessages,
        string ConversationId)
        : ScatterGatherApiMessageViewWithSystemMessagesContext(PagingInfo, SortInfo, IncludeSystemMessages);

    public class MessagesByConversationApi : ScatterGatherApiMessageView<IErrorMessageDataStore, MessagesByConversationContext>
    {
        public MessagesByConversationApi(IErrorMessageDataStore dataStore, Settings settings,
            IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : base(dataStore, settings,
            httpClientFactory, httpContextAccessor)
        {
        }

        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(MessagesByConversationContext input) =>
            DataStore.GetAllMessagesByConversation(input.ConversationId, input.PagingInfo, input.SortInfo,
                input.IncludeSystemMessages);
    }
}