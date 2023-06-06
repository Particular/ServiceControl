namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;

    public interface IRecoverabilityIngestionUnitOfWork
    {
        Task RecordFailedProcessingAttempt(string uniqueMessageId,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups);
        Task RecordSuccessfulRetry(string retriedMessageUniqueId);
    }
}