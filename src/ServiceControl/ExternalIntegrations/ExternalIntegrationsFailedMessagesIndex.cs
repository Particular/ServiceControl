namespace ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using MessageFailures;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts;

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
                        ? MessageFailed.MessageStatus.ArchivedFailure
                        : message.ProcessingAttempts.Count == 1
                            ? MessageFailed.MessageStatus.Failed
                            : MessageFailed.MessageStatus.RepeatedFailure,
                    ProcessingDetails = new MessageFailed.ProcessingInfo
                    {
                        SendingEndpoint = new MessageFailed.ProcessingInfo.Endpoint()
                        {
                            Host = sendingEndpoint.Host,
                            HostId = sendingEndpoint.HostId,
                            Name = sendingEndpoint.Name
                        },
                        ProcessingEndpoint = new MessageFailed.ProcessingInfo.Endpoint()
                        {
                            Host = receivingEndpoint.Host,
                            HostId = receivingEndpoint.HostId,
                            Name = receivingEndpoint.Name
                        },
                    },
                    MessageDetails = new MessageFailed.Message()
                    {
                        Headers = last.Headers,
                        ContentType = (string)last.MessageMetadata["ContentType"],
                        Body = (string)last.MessageMetadata["Body"],
                        MessageId = last.MessageId,
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
                            StackTrace = last.FailureDetails.Exception.StackTrace,
                        },
                    },
                });
            
            DisableInMemoryIndexing = true;
            StoreAllFields(FieldStorage.Yes);
        }
    }
}