namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Infrastructure.Extensions;
    using Nancy;
    using NServiceBus;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using SagaAudit;

    public class GetAllMessagesApi : ScatterGatherApiMessageView<NoInput>
    {
        public override async Task<QueryResult<List<MessagesView>>> LocalQuery(Request request, NoInput input)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .IncludeSystemMessagesWhere(request)
                    .Statistics(out var stats)
                    .Sort(request)
                    .Paging(request)
                    .Select(message => new MessagesView
                    {
                        Id = message.UniqueMessageId,
                        MessageId = message.MessageId,
                        MessageType = message.MessageType,
                        SendingEndpoint = (EndpointDetails)message.MessageMetadata["SendingEndpoint"],
                        ReceivingEndpoint = (EndpointDetails)message.MessageMetadata["ReceivingEndpoint"],
                        TimeSent = message.TimeSent,
                        ProcessedAt = message.ProcessedAt,
                        CriticalTime = message.CriticalTime,
                        ProcessingTime = message.ProcessingTime,
                        DeliveryTime = message.DeliveryTime,
                        IsSystemMessage = message.IsSystemMessage,
                        ConversationId = message.ConversationId,
                        //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                        // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                        Headers = message.Headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                        Status = message.Status,
                        MessageIntent = (MessageIntentEnum)message.MessageMetadata["MessageIntent"],
                        BodyUrl = (string)message.MessageMetadata["BodyUrl"],
                        BodySize = (int)message.MessageMetadata["ContentLength"],
                        InvokedSagas = (List<SagaInfo>)message.MessageMetadata["InvokedSagas"],
                        OriginatesFromSaga = (SagaInfo)message.MessageMetadata["OriginatesFromSaga"]
                    })
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<List<MessagesView>>(results, stats.ToQueryStatsInfo());
            }
        }
    }
}