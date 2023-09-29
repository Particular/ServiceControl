namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using ServiceControl.MessageFailures;

    public interface IRecoverabilityIngestionUnitOfWork
    {
        Task RecordFailedProcessingAttempt(MessageContext context,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups);

        Task RecordSuccessfulRetry(string retriedMessageUniqueId);
    }
}