namespace ServiceControl.ExternalIntegrations
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    public static class MessageFailedConverter
    {
        public static Contracts.MessageFailed ToEvent(this FailedMessage message)
        {
            var last = message.ProcessingAttempts.Last();
            var sendingEndpoint = GetNullableMetadataValue<EndpointDetails>(last.MessageMetadata, "SendingEndpoint");
            var receivingEndpoint = GetNullableMetadataValue<EndpointDetails>(last.MessageMetadata, "ReceivingEndpoint");

            return new Contracts.MessageFailed
            {
                FailedMessageId = message.UniqueMessageId,
                MessageType = GetNullableMetadataValue<string>(last.MessageMetadata,"MessageType"),
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status == FailedMessageStatus.Archived
                    ? Contracts.MessageFailed.MessageStatus.ArchivedFailure
                    : message.ProcessingAttempts.Count == 1
                        ? Contracts.MessageFailed.MessageStatus.Failed
                        : Contracts.MessageFailed.MessageStatus.RepeatedFailure,
                SendingEndpoint = sendingEndpoint != null ? new Contracts.MessageFailed.Endpoint
                {
                    Host = sendingEndpoint.Host,
                    HostId = sendingEndpoint.HostId,
                    Name = sendingEndpoint.Name
                } :null,
                ProcessingEndpoint = receivingEndpoint != null ? new Contracts.MessageFailed.Endpoint
                {
                    Host = receivingEndpoint.Host,
                    HostId = receivingEndpoint.HostId,
                    Name = receivingEndpoint.Name
                } : null,
                MessageDetails = new Contracts.MessageFailed.Message
                {
                    Headers = last.Headers,
                    ContentType = GetNullableMetadataValue<string>(last.MessageMetadata, "ContentType"),
                    Body = GetBody(last),
                    MessageId = last.MessageId,
                },
                FailureDetails = new Contracts.MessageFailed.FailureInfo
                {
                    AddressOfFailingEndpoint = last.FailureDetails.AddressOfFailingEndpoint,
                    TimeOfFailure = last.FailureDetails.TimeOfFailure,
                    Exception = new Contracts.MessageFailed.FailureInfo.ExceptionInfo
                    {
                        ExceptionType = last.FailureDetails.Exception.ExceptionType,
                        Message = last.FailureDetails.Exception.Message,
                        Source = last.FailureDetails.Exception.Source,
                        StackTrace = last.FailureDetails.Exception.StackTrace,
                    },
                },
            };
        }

        static T GetNullableMetadataValue<T>(IReadOnlyDictionary<string, object> metadata, string key)
            where T : class
        {
            object value;
            metadata.TryGetValue(key, out value);
            return (T)value;
        }

        static string GetBody(FailedMessage.ProcessingAttempt last)
        {
            object body;
            if (last.MessageMetadata.TryGetValue("Body", out body))
            {
                return (string)body;
            }
            return null;
        }
    }
}