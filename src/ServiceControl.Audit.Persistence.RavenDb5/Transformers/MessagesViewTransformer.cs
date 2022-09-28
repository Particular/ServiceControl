namespace ServiceControl.Audit.Persistence.RavenDb.Transformers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Auditing;
    using Auditing.MessagesView;
    using Indexes;
    using Monitoring;
    using NServiceBus;
    using Raven.Client.Documents;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.SagaAudit;

    static class MessagesViewTransformerExtensions
    {
        //TODO: figure out headers and everything else ;) 
        public static IQueryable<MessagesView> ToMessagesView(
            this IQueryable<MessagesViewIndex.SortAndFilterOptions> messages)
        => messages.OfType<ProcessedMessage>()
            .Select(message => new MessagesView
            {
                Id = message.UniqueMessageId,
                MessageId = message.MessageMetadata["MessageId"].ToString(),
                MessageType = message.MessageMetadata["MessageType"].ToString(),
                SendingEndpoint = (EndpointDetails)message.MessageMetadata["SendingEndpoint"],
                ReceivingEndpoint = (EndpointDetails)message.MessageMetadata["ReceivingEndpoint"],
                TimeSent = (DateTime)message.MessageMetadata["TimeSent"],
                ProcessedAt = message.ProcessedAt,
                CriticalTime = (TimeSpan)message.MessageMetadata["CriticalTime"],
                ProcessingTime = (TimeSpan)message.MessageMetadata["ProcessingTime"],
                DeliveryTime = (TimeSpan)message.MessageMetadata["DeliveryTime"],
                IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                ConversationId = message.MessageMetadata["ConversationId"].ToString(),
                Headers = message.Headers.ToArray(),
                Status = (bool)message.MessageMetadata["IsRetried"] ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                MessageIntent = (MessageIntentEnum)message.MessageMetadata["MessageIntent"],
                BodyUrl = message.MessageMetadata["BodyUrl"].ToString(),
                BodySize = (int)message.MessageMetadata["ContentLength"],
                InvokedSagas = (List<SagaInfo>)message.MessageMetadata["InvokedSagas"],
                OriginatesFromSaga = (SagaInfo)message.MessageMetadata["OriginatesFromSaga"]
            })
            .As<MessagesView>();
    }
}