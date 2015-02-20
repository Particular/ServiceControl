namespace Particular.Backend.Debugging.RavenDB.Migration
{
    using System;
    using System.Linq;
    using Particular.Backend.Debugging.RavenDB.Storage;
    using ServiceControl.Contracts.Operations;

    public class FailedMessageConverter
    {
        public MessageSnapshotDocument Convert(OldFailedMessage failedMessage)
        {
            var lastAttempt = failedMessage.ProcessingAttempts.Last();
            object body;
            lastAttempt.MessageMetadata.TryGetValue("Body", out body);
            var result = new MessageSnapshotDocument()
            {
                Id = failedMessage.Id,
                AttemptedAt = lastAttempt.AttemptedAt,
                ConversationId = lastAttempt.CorrelationId,
                IsSystemMessage = (bool)lastAttempt.MessageMetadata["IsSystemMessage"],
                MessageType = (string)lastAttempt.MessageMetadata["MessageType"],
                Body = new BodyInformation()
                {
                    BodyUrl = (string)lastAttempt.MessageMetadata["BodyUrl"],
                    ContentType = (string)lastAttempt.MessageMetadata["ContentType"],
                    ContentLenght = (int)lastAttempt.MessageMetadata["ContentLength"],
                    Text = (string)body
                },
                MessageIntent = lastAttempt.MessageIntent,
                Processing = new ProcessingStatistics()
                {
                    TimeSent = (DateTime)lastAttempt.MessageMetadata["TimeSent"],
                    CriticalTime = (TimeSpan)lastAttempt.MessageMetadata["CriticalTime"],
                    DeliveryTime = (TimeSpan)lastAttempt.MessageMetadata["DeliveryTime"],
                    ProcessingTime = (TimeSpan)lastAttempt.MessageMetadata["ProcessingTime"],
                },
                ReceivingEndpoint = (EndpointDetails)lastAttempt.MessageMetadata["ReceivingEndpoint"],
                SendingEndpoint = (EndpointDetails)lastAttempt.MessageMetadata["SendingEndpoint"],
            };
            result.Initialize(lastAttempt.MessageId, failedMessage.UniqueMessageId, ConvertStatus(failedMessage.Status));
            foreach (var header in lastAttempt.Headers)
            {
                result.Headers[header.Key] = header.Value;
            }
            return result;
        }

        MessageStatus ConvertStatus(FailedMessageStatus status)
        {
            switch (status)
            {
                case FailedMessageStatus.Archived:
                    return MessageStatus.ArchivedFailure;
                case FailedMessageStatus.Resolved:
                    return MessageStatus.ResolvedSuccessfully;
                default:
                    return MessageStatus.Failed;
            }
        }
    }
}