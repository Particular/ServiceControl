namespace ServiceControl.Migrations
{
    using System;
    using System.Linq;
    using Particular.Backend.Debugging;
    using Particular.Backend.Debugging.RavenDB.Model;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public class FailedMessageToMessageSnapshotDocumentConverter
    {
        public MessageSnapshotDocument Convert(FailedMessage failedMessage)
        {
            var lastAttempt = failedMessage.ProcessingAttempts.Last();
            object body;
            lastAttempt.MessageMetadata.TryGetValue("Body", out body);
            var result = new MessageSnapshotDocument()
            {
                Id = MessageSnapshotDocument.MakeDocumentId(failedMessage.UniqueMessageId),
                AttemptedAt = lastAttempt.AttemptedAt,
                ProcessedAt = lastAttempt.AttemptedAt,
                ConversationId = lastAttempt.CorrelationId,
                IsSystemMessage = (bool)lastAttempt.MessageMetadata["IsSystemMessage"],
                MessageType = (string)lastAttempt.MessageMetadata["MessageType"],
                Body = new BodyInformation()
                {
                    BodyUrl = (string)lastAttempt.MessageMetadata["BodyUrl"],
                    ContentType = (string)lastAttempt.MessageMetadata["ContentType"],
                    ContentLenght = (int)(long)lastAttempt.MessageMetadata["ContentLength"],
                    Text = (string)body
                },
                MessageIntent = lastAttempt.MessageIntent,
                Processing = new ProcessingStatistics()
                {
                    TimeSent = DateTime.Parse((string)lastAttempt.MessageMetadata["TimeSent"]),
                    CriticalTime = TimeSpan.Parse((string)lastAttempt.MessageMetadata["CriticalTime"]),
                    DeliveryTime = TimeSpan.Parse((string)lastAttempt.MessageMetadata["DeliveryTime"]),
                    ProcessingTime = TimeSpan.Parse((string)lastAttempt.MessageMetadata["ProcessingTime"]),
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