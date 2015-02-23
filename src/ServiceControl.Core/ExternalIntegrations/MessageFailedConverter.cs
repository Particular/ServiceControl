namespace ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using ServiceControl.MessageFailures;

    public static class MessageFailedConverter
    {
        public static Contracts.MessageFailed ToEvent(this MessageFailureHistory message)
        {
            var last = message.ProcessingAttempts.Last();
            var sendingEndpoint = last.SendingEndpoint;
            var receivingEndpoint = last.ProcessingEndpoint;

            return new Contracts.MessageFailed
            {
                FailedMessageId = message.UniqueMessageId,
                MessageType = last.MessageType,
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status == FailedMessageStatus.Archived
                    ? Contracts.MessageFailed.MessageStatus.ArchivedFailure
                    : message.ProcessingAttempts.Count == 1
                        ? Contracts.MessageFailed.MessageStatus.Failed
                        : Contracts.MessageFailed.MessageStatus.RepeatedFailure,
                SendingEndpoint = new Contracts.MessageFailed.Endpoint
                {
                    Host = sendingEndpoint.Host,
                    //HostId = sendingEndpoint.HostId,TODO
                    Name = sendingEndpoint.Name
                },
                ProcessingEndpoint = new Contracts.MessageFailed.Endpoint
                {
                    Host = receivingEndpoint.Host,
                    //HostId = receivingEndpoint.HostId,TODO
                    Name = receivingEndpoint.Name
                },
                MessageDetails = new Contracts.MessageFailed.Message
                {
                    Headers = last.Headers,
                    ContentType = last.ContentType,
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
    }
}