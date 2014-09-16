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

    public class ExternalIntegrationsFailedMessagesViewIndex : AbstractMultiMapIndexCreationTask<MessageFailed>
    {        
        public ExternalIntegrationsFailedMessagesViewIndex()
        {            
            AddMap<FailedMessage>(messages => from message in messages
                where message.Status != FailedMessageStatus.Resolved
                let last = message.ProcessingAttempts.Last()
                select new MessageFailed()
                {
                    EntityId = message.UniqueMessageId,
                    MessageId = last.MessageId,
                    MessageType = (string)last.MessageMetadata["MessageType"], 
                    IsSystemMessage = (bool)last.MessageMetadata["IsSystemMessage"],
                    Status = message.Status == FailedMessageStatus.Archived
                        ? MessageStatus.ArchivedFailure
                        : message.ProcessingAttempts.Count == 1
                            ? MessageStatus.Failed
                            : MessageStatus.RepeatedFailure,
                    Headers = last.Headers,
                    NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                    FailureDetails = message.ProcessingAttempts.Select(pa => new FailureDetails
                    {
                        AddressOfFailingEndpoint = pa.FailureDetails.AddressOfFailingEndpoint,
                        TimeOfFailure = pa.FailureDetails.TimeOfFailure,
                        Exception = new ExceptionDetails
                        {
                            ExceptionType = pa.FailureDetails.Exception.ExceptionType,
                            Message = pa.FailureDetails.Exception.Message,
                            Source = pa.FailureDetails.Exception.Source,
                            StackTrace = pa.FailureDetails.Exception.StackTrace
                        }
                    }).ToList(),
                    Body = (string)last.MessageMetadata["Body"],
                    CorrelationId = (string)last.MessageMetadata["CorrelationId"],
                });
            
            DisableInMemoryIndexing = true;
            StoreAllFields(FieldStorage.Yes);
        }
    }
}