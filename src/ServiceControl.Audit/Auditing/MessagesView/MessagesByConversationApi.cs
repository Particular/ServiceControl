namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Infrastructure.SQL;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.Infrastructure.Extensions;

    class MessagesByConversationApi : ApiBase<string, IList<MessagesView>>
    {
        public MessagesByConversationApi(SqlQueryStore queryStore) : base(queryStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request, string conversationId)
        {
            var results = await Store.MessagesByConversation(request, out var stats).ConfigureAwait(false);

            return new QueryResult<IList<MessagesView>>(results, stats);
        }
    }
}