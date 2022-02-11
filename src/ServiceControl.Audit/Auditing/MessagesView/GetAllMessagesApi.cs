namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Infrastructure.SQL;
    using Raven.Client;
    using ServiceControl.Infrastructure.Extensions;

    class GetAllMessagesApi : ApiBaseNoInput<IList<MessagesView>>
    {
        public GetAllMessagesApi(SqlQueryStore queryStore) : base(queryStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request)
        {
            var results = await Store.GetAllMessages(request, out var stats).ConfigureAwait(false);

            return new QueryResult<IList<MessagesView>>(results, stats);
        }
    }
}