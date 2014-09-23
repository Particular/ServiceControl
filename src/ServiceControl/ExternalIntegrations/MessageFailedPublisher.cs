namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;

    public class MessageFailedPublisher : EventPublisher<MessageFailed, MessageFailedPublisher.Reference>
    {
        protected override Reference CreateReference(MessageFailed evnt)
        {
            return new Reference
            {
                FailedMessageId = new Guid(evnt.FailedMessageId)
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<Reference> references, IDocumentSession session)
        {
            var documentIds = references.Select(x => x.FailedMessageId).Cast<ValueType>().ToArray();
            var failedMessageData = session.Load<FailedMessage>(documentIds);
            return failedMessageData.Select(ConvertToEvent);
        }

        public class Reference
        {
            public Guid FailedMessageId { get; set; }
        }

        private static Contracts.MessageFailed ConvertToEvent(FailedMessage message)
        {
            var last = message.ProcessingAttempts.Last();
            var sendingEndpoint = (Contracts.Operations.EndpointDetails)last.MessageMetadata["SendingEndpoint"];
            var receivingEndpoint = (Contracts.Operations.EndpointDetails)last.MessageMetadata["ReceivingEndpoint"];
            return new Contracts.MessageFailed()
            {
                FailedMessageId = message.UniqueMessageId,
                MessageType = (string)last.MessageMetadata["MessageType"],
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status == FailedMessageStatus.Archived
                    ? Contracts.MessageFailed.MessageStatus.ArchivedFailure
                    : message.ProcessingAttempts.Count == 1
                        ? Contracts.MessageFailed.MessageStatus.Failed
                        : Contracts.MessageFailed.MessageStatus.RepeatedFailure,
                ProcessingDetails = new Contracts.MessageFailed.ProcessingInfo
                {
                    SendingEndpoint = new Contracts.MessageFailed.ProcessingInfo.Endpoint()
                    {
                        Host = sendingEndpoint.Host,
                        HostId = sendingEndpoint.HostId,
                        Name = sendingEndpoint.Name
                    },
                    ProcessingEndpoint = new Contracts.MessageFailed.ProcessingInfo.Endpoint()
                    {
                        Host = receivingEndpoint.Host,
                        HostId = receivingEndpoint.HostId,
                        Name = receivingEndpoint.Name
                    },
                },
                MessageDetails = new Contracts.MessageFailed.Message()
                {
                    Headers = last.Headers,
                    ContentType = (string)last.MessageMetadata["ContentType"],
                    Body = (string)last.MessageMetadata["Body"],
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