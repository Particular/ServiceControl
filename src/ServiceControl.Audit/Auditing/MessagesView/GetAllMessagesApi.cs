namespace ServiceControl.Audit.Auditing.MessagesView
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Extensions;
    using Monitoring;
    using NServiceBus;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using SagaAudit;

    class GetAllMessagesApi : ApiBaseNoInput<IList<MessagesView>>
    {
        public GetAllMessagesApi(IDocumentStore documentStore) : base(documentStore)
        {
        }

        protected override async Task<QueryResult<IList<MessagesView>>> Query(HttpRequestMessage request)
        {
            // TODO: RAVEN5 - No Transformers
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<ProcessedMessage>()
                    //.IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    //.Sort(request)
                    //.Paging(request)
                    .ToMessagesView()
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }
    }

    static class Ext
    {
        public static IQueryable<MessagesView> ToMessagesView(this IRavenQueryable<ProcessedMessage> query)
            => query.Select(message => new
                {
                    Id = message.UniqueMessageId,
                    MessageId = (string)message.MessageMetadata["MessageId"],
                    MessageType = (string)message.MessageMetadata["MessageType"],
                    SendingEndpoint = (EndpointDetails)message.MessageMetadata["SendingEndpoint"],
                    ReceivingEndpoint = (EndpointDetails)message.MessageMetadata["ReceivingEndpoint"],
                    TimeSent = (DateTime?)message.MessageMetadata["TimeSent"],
                    ProcessedAt = message.ProcessedAt,
                    CriticalTime = (TimeSpan)message.MessageMetadata["CriticalTime"],
                    ProcessingTime = (TimeSpan)message.MessageMetadata["ProcessingTime"],
                    DeliveryTime = (TimeSpan)message.MessageMetadata["DeliveryTime"],
                    IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                    ConversationId = (string)message.MessageMetadata["ConversationId"],
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    //Headers = message.Headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                    Headers = message.Headers.ToArray(),
                    Status = !(bool)message.MessageMetadata["IsRetried"] ? MessageStatus.Successful : MessageStatus.ResolvedSuccessfully,
                    MessageIntent = (MessageIntentEnum)message.MessageMetadata["MessageIntent"],
                    BodyUrl = (string)message.MessageMetadata["BodyUrl"],
                    BodySize = (int)message.MessageMetadata["ContentLength"],
                    InvokedSagas = (List<SagaInfo>)message.MessageMetadata["InvokedSagas"],
                    OriginatesFromSaga = (SagaInfo)message.MessageMetadata["OriginatesFromSaga"]
                })
                .As<MessagesView>();
    }

}