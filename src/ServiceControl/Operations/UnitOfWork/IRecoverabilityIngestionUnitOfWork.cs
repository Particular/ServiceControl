namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;

    interface IRecoverabilityIngestionUnitOfWork
    {
        Task RecordFailedProcessingAttempt(string uniqueMessageId,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups);
        Task RecordSuccessfulRetry(string retriedMessageUniqueId);
    }
}