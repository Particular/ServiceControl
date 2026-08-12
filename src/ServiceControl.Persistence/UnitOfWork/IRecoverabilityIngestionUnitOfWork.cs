namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using ServiceControl.MessageFailures;

    public interface IRecoverabilityIngestionUnitOfWork
    {
        Task RecordFailedProcessingAttempt(MessageContext context,
            FailedMessage.ProcessingAttempt processingAttempt,
            List<FailedMessage.FailureGroup> groups, CancellationToken cancellationToken = default);

        Task RecordSuccessfulRetry(string retriedMessageUniqueId, CancellationToken cancellationToken = default);
    }
}