namespace ServiceControl.Contracts.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Scheduling.Messages;

    public class PhysicalMessage
    {
        public PhysicalMessage()
        {
            
        }

        public PhysicalMessage(TransportMessage message)
        {
            MessageId = message.Id;
            Headers = message.Headers;
            Body = message.Body;
            ReplyToAddress = message.ReplyToAddress.ToString();
            CorrelationId = message.CorrelationId;
            Recoverable = message.Recoverable;
            MessageIntent = message.MessageIntent;

            string conversationId;

            if (message.Headers.TryGetValue(NServiceBus.Headers.ConversationId, out conversationId))
            {
                ConversationId = conversationId;
            }

            string timeSent;

            if (message.Headers.TryGetValue(NServiceBus.Headers.TimeSent, out timeSent))
            {
                TimeSent = DateTimeExtensions.ToUtcDateTime(timeSent);
            }

            if (Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                IsSystemMessage = true;
                MessageType = "SystemMessage";
            }

            string enclosedMessageTypes;

            if (message.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out enclosedMessageTypes))
            {
                MessageType = GetMessageType(enclosedMessageTypes);
                IsSystemMessage = DetectSystemMessage(MessageType);
            }
        }

        bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains(typeof(ScheduledTask).FullName);
        }

        string GetMessageType(string messageTypeString)
        {
            if (!messageTypeString.Contains(","))
            {
                return messageTypeString;
            }

            return messageTypeString.Split(',').First();
        }

        public bool IsSystemMessage { get; set; }

        public string MessageId { get; set; }

        public string ConversationId { get; set; }
        public byte[] Body { get; set; }

        public Dictionary<string, string> Headers { get; set; }
        public string ReplyToAddress { get; set; }
        public string CorrelationId { get; set; }
        public bool Recoverable { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
        public DateTime TimeSent { get; set; }
        public string MessageType { get; set; }
    }
}