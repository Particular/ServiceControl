namespace ServiceControl.Migrations
{
    using System;
    using NServiceBus;
    using Particular.Backend.Debugging;
    using Particular.Backend.Debugging.RavenDB.Model;
    using ServiceControl.Contracts.Operations;

    public class ProcessedMessageConverter
    {
        public MessageSnapshotDocument Convert(OldProcessedMessage processedMessage)
        {
            object body;
            processedMessage.MessageMetadata.TryGetValue("Body", out body);
            var result = new MessageSnapshotDocument()
            {
                Id = processedMessage.Id,
                AttemptedAt = processedMessage.ProcessedAt,
                ConversationId = processedMessage.Headers["NServiceBus.CorrelationId"],
                IsSystemMessage = (bool)processedMessage.MessageMetadata["IsSystemMessage"],
                MessageType = (string)processedMessage.MessageMetadata["MessageType"],
                Body = new BodyInformation()
                {
                    BodyUrl = (string)processedMessage.MessageMetadata["BodyUrl"],
                    ContentType = (string)processedMessage.MessageMetadata["ContentType"],
                    ContentLenght = (int)processedMessage.MessageMetadata["ContentLength"],
                    Text = (string)body
                },
                MessageIntent = (MessageIntentEnum)(int)processedMessage.MessageMetadata["MessageIntent"],
                Processing = new ProcessingStatistics()
                {
                    TimeSent = (DateTime)processedMessage.MessageMetadata["TimeSent"],
                    CriticalTime = (TimeSpan)processedMessage.MessageMetadata["CriticalTime"],
                    DeliveryTime = (TimeSpan)processedMessage.MessageMetadata["DeliveryTime"],
                    ProcessingTime = (TimeSpan)processedMessage.MessageMetadata["ProcessingTime"],
                },
                ReceivingEndpoint = (EndpointDetails)processedMessage.MessageMetadata["ReceivingEndpoint"],
                SendingEndpoint = (EndpointDetails)processedMessage.MessageMetadata["SendingEndpoint"],
            };
            result.Initialize((string)processedMessage.MessageMetadata["MessageId"], processedMessage.UniqueMessageId, MessageStatus.Successful);
            foreach (var header in processedMessage.Headers)
            {
                result.Headers[header.Key] = header.Value;
            }
            return result;
        }
    }
}