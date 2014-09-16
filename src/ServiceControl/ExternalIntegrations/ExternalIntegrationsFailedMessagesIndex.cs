namespace ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using MessageFailures;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Failures;
    using ExceptionDetails = ServiceControl.Contracts.Failures.ExceptionDetails;
    using FailedMessageStatus = ServiceControl.MessageFailures.FailedMessageStatus;
    using FailureDetails = ServiceControl.Contracts.Failures.FailureDetails;
    using MessageStatus = ServiceControl.Contracts.Failures.MessageStatus;

    public class ExternalIntegrationsFailedMessagesIndex : AbstractMultiMapIndexCreationTask<MessageFailed>
    {        
        public ExternalIntegrationsFailedMessagesIndex()
        {            
            AddMap<FailedMessage>(messages => from message in messages
                where message.Status != FailedMessageStatus.Resolved
                let last = message.ProcessingAttempts.Last()
                let sendingEndpoint = (Contracts.Operations.EndpointDetails) last.MessageMetadata["SendingEndpoint"]
                let receivingEndpoint = (Contracts.Operations.EndpointDetails) last.MessageMetadata["ReceivingEndpoint"]
                select new MessageFailed()
                {
                    FailedMessageId = message.UniqueMessageId,
                    MessageType = (string)last.MessageMetadata["MessageType"],
                    NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                    Status = message.Status == FailedMessageStatus.Archived
                        ? MessageStatus.ArchivedFailure
                        : message.ProcessingAttempts.Count == 1
                            ? MessageStatus.Failed
                            : MessageStatus.RepeatedFailure,
                    ProcessingDetails = new ProcessingDetails
                    {
                        MessageId = last.MessageId,
                        SendingEndpoint = new EndpointDetails()
                        {
                            Host = sendingEndpoint.Host,
                            HostId = sendingEndpoint.HostId,
                            Name = sendingEndpoint.Name
                        },
                        ProcessingEndpoint = new EndpointDetails()
                        {
                            Host = receivingEndpoint.Host,
                            HostId = receivingEndpoint.HostId,
                            Name = receivingEndpoint.Name
                        }
                    },
                    MessageDetails = new MessageDetails()
                    {
                        Headers = last.Headers,
                        ContentType = (string)last.MessageMetadata["ContentType"],
                        Body = (string)last.MessageMetadata["Body"],                       
                        BodyUrl = (string)last.MessageMetadata["BodyUrl"],                       
                    },
                    FailureDetails = new FailureDetails
                    {
                        AddressOfFailingEndpoint = last.FailureDetails.AddressOfFailingEndpoint,
                        TimeOfFailure = last.FailureDetails.TimeOfFailure,
                        Exception = new ExceptionDetails
                        {
                            ExceptionType = last.FailureDetails.Exception.ExceptionType,
                            Message = last.FailureDetails.Exception.Message,
                            Source = last.FailureDetails.Exception.Source,
                            StackTrace = last.FailureDetails.Exception.StackTrace
                        }
                    }
                });
            
            DisableInMemoryIndexing = true;
            StoreAllFields(FieldStorage.Yes);
        }
    }
}