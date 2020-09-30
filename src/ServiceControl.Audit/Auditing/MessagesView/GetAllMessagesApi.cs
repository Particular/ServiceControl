namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Raven.Client.Documents;
    using ServiceControl.Infrastructure.Extensions;

    class GetAllMessagesApi : ApiBaseNoInput<IList<MessagesView>>
    {
        public GetAllMessagesApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<ProcessedMessage, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .Select(x => new MessagesView
                    {
                        ProcessingTime = x.ProcessingTime,
                        SendingEndpoint = x.SendingEndpoint,
                        ReceivingEndpoint = x.ReceivingEndpoint,
                        MessageType = x.MessageType,
                        MessageId = x.MessageId,
                        ConversationId = x.ConversationId,
                        DeliveryTime = x.DeliveryTime,
                        TimeSent = x.TimeSent,
                        CriticalTime = x.CriticalTime,
                        IsSystemMessage = x.IsSystemMessage,
                        Status = x.Status,
                        ProcessedAt = x.ProcessedAt,
                        Headers = x.Headers.ToArray(),
                        MessageIntent = x.MessageIntent,
                        InvokedSagas = x.InvokedSagas,
                        OriginatesFromSaga = x.OriginatesFromSaga,
                        BodyUrl = x.BodyUrl,
                        BodySize = x.BodySize,
                        Id = x.Id
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }
    }
}