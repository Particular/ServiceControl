namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
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

        /// <summary>
        /// Resolves the message the retry was for, unless an attempt made after
        /// <paramref name="succeededAt" /> has already been recorded. That attempt means the message
        /// failed again after the retry succeeded, and the two can reach storage in either order,
        /// from separate batches or from separate ingestion instances.
        /// </summary>
        Task RecordSuccessfulRetry(string retriedMessageUniqueId, DateTime succeededAt, CancellationToken cancellationToken = default);
    }
}