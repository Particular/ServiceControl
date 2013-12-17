namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using NServiceBus;

    public class MessagesView
    {
        public string Id { get; set; }
        public string ReceivingEndpointName { get; set; }
        public string MessageId { get; set; }
        public string MessageType { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime ProcessedAt { get; set; }
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool IsSystemMessage { get; set; }
        public string ConversationId { get; set; }
        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }
        public string[] Query { get; set; }
        public MessageStatus Status { get; set; }
        public string SendingEndpointName { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
    }
}