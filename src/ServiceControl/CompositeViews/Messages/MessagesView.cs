namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using NServiceBus;
    using SagaAudit;

    public class MessagesView
    {
        public string Id { get; set; }
        public string MessageId { get; set; }
        public string MessageType { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
        public DateTime? TimeSent { get; set; }
        public DateTime ProcessedAt { get; set; }
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public TimeSpan DeliveryTime { get; set; }
        public bool IsSystemMessage { get; set; }
        public string ConversationId { get; set; }
        public IEnumerable<KeyValuePair<string, object>> Headers { get; set; }
        public MessageStatus Status { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
        public string BodyUrl { get; set; }
        public int BodySize { get; set; }
        public List<SagaInfo> InvokedSagas { get; set; }
        public SagaInfo OriginatesFromSaga { get; set; }
        public string InstanceId { get; set; }
    }
}