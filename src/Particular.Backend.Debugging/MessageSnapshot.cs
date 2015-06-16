namespace Particular.Backend.Debugging
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using Particular.Backend.Debugging.Api;
    using ServiceControl.Contracts.Operations;

    public class MessageSnapshot
    {
        public MessageSnapshot()
        {
            Headers = new Dictionary<string, string>();
        }

        public void Initialize(string id, string uniqueId, MessageStatus initialStatus)
        {
            MessageId = id;
            UniqueMessageId = uniqueId;
            Status = initialStatus;
        }

        public DateTime AttemptedAt { get; set; } //former processed at

        //New
        public MessageStatus Status { get; set; }
        public string MessageId { get; protected set; }
        public string UniqueMessageId { get; protected set; }
        public Dictionary<string, string> Headers { get; protected set; }
        public ProcessingStatistics Processing { get; set; }
        public BodyInformation Body { get; set; }
        public SagaInformation Sagas { get; set; }
        public string ConversationId { get; set; }
        public string RelatedToId { get; set; }
        public string MessageType { get; set; }
        public bool IsSystemMessage { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
        public string HeadersForSearching { get; set; }
    }

    public class ProcessingStatistics
    {
        public DateTime TimeSent { get; set; }
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public TimeSpan DeliveryTime { get; set; }
    }


    public class BodyInformation
    {
        public string ContentType { get; set; }
        public int ContentLength { get; set; }
        public string BodyUrl { get; set; }
        public string Text { get; set; }
    }

    public class SagaInformation
    {
        public List<SagaInfo> InvokedSagas { get; set; }
        public SagaInfo OriginatesFromSaga { get; set; }
    }
}