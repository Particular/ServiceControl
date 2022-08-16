namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    class MessagesByConversationApi : ApiBase<string, IList<MessagesView>>
    {
        public MessagesByConversationApi(IAuditDataStore dataStore) : base(dataStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, string conversationId)
        {
            return await DataStore.QueryMessagesByConversationId(request, conversationId).ConfigureAwait(false);
        }
    }
}