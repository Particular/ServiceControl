namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using Monitoring;
    using NServiceBus;
    using ServiceControl.SagaAudit;

    public class ProcessedMessage
    {
        public ProcessedMessage()
        {
            SearchTerms = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();
        }

        public ProcessedMessage(Dictionary<string, string> headers, Dictionary<string, string> searchTerms)
        {
            UniqueMessageId = headers.UniqueId();
            Headers = headers;
            SearchTerms = searchTerms;

            if (Headers.TryGetValue(NServiceBus.Headers.ProcessingEnded, out var processedAt))
            {
                ProcessedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
            }
            else
            {
                ProcessedAt = DateTime.UtcNow; // best guess
            }
        }

        public string Id { get; set; }

        public string UniqueMessageId { get; set; }

        public string MessageId { get; set; }
        public string MessageType { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
        public DateTime? TimeSent { get; set; }
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public TimeSpan DeliveryTime { get; set; }
        public bool IsSystemMessage { get; set; }
        public string ConversationId { get; set; }
        public MessageStatus Status { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
        public string BodyUrl { get; set; }//
        public int BodySize { get; set; }//
        public List<SagaInfo> InvokedSagas { get; set; }
        public SagaInfo OriginatesFromSaga { get; set; }

        public Dictionary<string, string> Headers { get; set; }
        public Dictionary<string, string> SearchTerms { get; }

        public DateTime ProcessedAt { get; set; }
        public string ContentType { get; set; }
    }
}