using System;
using System.Collections.Generic;
using NServiceBus;
using ServiceControl.Audit.Monitoring;
using ServiceControl.SagaAudit;

namespace ServiceControl.Audit.Auditing
{
    public class ProcessedMessageData
    {
        public string ContentType { get; set; }
        public string MessageId { get; set; }
        public string MessageType { get; set; }
        public bool IsSystemMessage { get; set; }
        public DateTime? TimeSent { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public TimeSpan DeliveryTime { get; set; }
        public string ConversationId { get; set; }
        public bool IsRetried { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
        public string BodyUrl { get; set; }
        public int ContentLength { get; set; }
        public List<SagaInfo> InvokedSagas { get; set; }
        public SagaInfo OriginatesFromSaga { get; set; }
        public string RelatedToId { get; set; } //Not included in results
        public string Body { get; set; } //For text search only
    }
}