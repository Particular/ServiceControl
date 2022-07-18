namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using Monitoring;

    interface IIngestionUnitOfWork
    {
        void RecordKnownEndpoint(KnownEndpoint knownEndpoint);
        void RecordFailedProcessingAttempt(string uniqueMessageId,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups);
        void RecordSuccessfulRetry(string retriedMessageUniqueId);
        Task Complete();
    }
}