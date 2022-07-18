namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using MessageFailures;

    interface IRecoverabilityIngestionUnitOfWork
    {
        void RecordFailedProcessingAttempt(string uniqueMessageId,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups);
        void RecordSuccessfulRetry(string retriedMessageUniqueId);
    }
}