namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using Contracts;
    using Contracts.Operations;
    using MessageFailures;

    public static class MessageFailedConverter
    {
        public static MessageFailed ToEvent(this FailedMessage message)
        {
            var last = message.ProcessingAttempts.Last();
            var sendingEndpoint = GetNullableMetadataValue<EndpointDetails>(last.MessageMetadata, "SendingEndpoint");
            var receivingEndpoint = GetNullableMetadataValue<EndpointDetails>(last.MessageMetadata, "ReceivingEndpoint");

            return new MessageFailed
            {
                FailedMessageId = message.UniqueMessageId,
                MessageType = GetNullableMetadataValue<string>(last.MessageMetadata, "MessageType"),
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status == FailedMessageStatus.Archived
                    ? MessageFailed.MessageStatus.ArchivedFailure
                    : message.ProcessingAttempts.Count == 1
                        ? MessageFailed.MessageStatus.Failed
                        : MessageFailed.MessageStatus.RepeatedFailure,
                SendingEndpoint = sendingEndpoint != null
                    ? new MessageFailed.Endpoint
                    {
                        Host = sendingEndpoint.Host,
                        HostId = sendingEndpoint.HostId,
                        Name = sendingEndpoint.Name
                    }
                    : null,
                ProcessingEndpoint = receivingEndpoint != null
                    ? new MessageFailed.Endpoint
                    {
                        Host = receivingEndpoint.Host,
                        HostId = receivingEndpoint.HostId,
                        Name = receivingEndpoint.Name
                    }
                    : null,
                MessageDetails = new MessageFailed.Message
                {
                    Headers = last.Headers,
                    ContentType = GetNullableMetadataValue<string>(last.MessageMetadata, "ContentType"),
                    Body = GetBody(last),
                    MessageId = last.MessageId
                },
                FailureDetails = new MessageFailed.FailureInfo
                {
                    AddressOfFailingEndpoint = last.FailureDetails.AddressOfFailingEndpoint,
                    TimeOfFailure = last.FailureDetails.TimeOfFailure,
                    Exception = new MessageFailed.FailureInfo.ExceptionInfo
                    {
                        ExceptionType = last.FailureDetails.Exception.ExceptionType,
                        Message = last.FailureDetails.Exception.Message,
                        Source = last.FailureDetails.Exception.Source,
                        StackTrace = last.FailureDetails.Exception.StackTrace
                    }
                }
            };
        }

        static T GetNullableMetadataValue<T>(IReadOnlyDictionary<string, object> metadata, string key)
            where T : class
        {
            metadata.TryGetValue(key, out var value);
            return (T)value;
        }

        static string GetBody(FailedMessage.ProcessingAttempt last)
        {
            if (last.MessageMetadata.TryGetValue("Body", out var body))
            {
                return (string)body;
            }

            return null;
        }
    }
}