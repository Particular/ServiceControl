namespace ServiceControl.MessageFailures.Migration
{
    using System;
    using System.Linq;
    using ServiceControl.Contracts.Operations;

    public class Converter
    {
        public FailedMessage Convert(OldFailedMessage message)
        {
            return new FailedMessage()
            {
                Id = message.Id,
                Status = message.Status,
                UniqueMessageId = message.UniqueMessageId,
                ProcessingAttempts = message.ProcessingAttempts.Select(Convert).ToList()
            };
        }

        static FailedMessage.ProcessingAttempt Convert(OldFailedMessage.ProcessingAttempt attempt)
        {
            return new FailedMessage.ProcessingAttempt()
            {
                FailureDetails = attempt.FailureDetails,
                CorrelationId = attempt.CorrelationId,
                AttemptedAt = attempt.AttemptedAt,
                MessageId = attempt.MessageId,
                Headers = attempt.Headers,
                ReplyToAddress = attempt.ReplyToAddress,
                Recoverable = attempt.Recoverable,
                MessageIntent = attempt.MessageIntent.ToString(),
                SendingEndpoint = (EndpointDetails)attempt.MessageMetadata["SendingEndpoint"],
                ProcessingEndpoint = (EndpointDetails)attempt.MessageMetadata["ReceivingEndpoint"],
                ContentType = (string) attempt.MessageMetadata["ContentType"],
                IsSystemMessage = (bool)attempt.MessageMetadata["IsSystemMessage"],
                MessageType = (string)attempt.MessageMetadata["MessageType"],
                TimeSent = (DateTime)attempt.MessageMetadata["TimeSent"],
                HeadersForSearching = (string)attempt.MessageMetadata["HeadersForSearching"]
            };
        }
    }
}