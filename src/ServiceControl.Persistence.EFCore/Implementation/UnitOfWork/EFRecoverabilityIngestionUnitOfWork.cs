namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using NServiceBus.Transport;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.UnitOfWork;

public class EFRecoverabilityIngestionUnitOfWork : IRecoverabilityIngestionUnitOfWork
{
    public Task RecordFailedProcessingAttempt(MessageContext context,
        FailedMessage.ProcessingAttempt processingAttempt,
        List<FailedMessage.FailureGroup> groups) =>
        throw new NotImplementedException();

    public Task RecordSuccessfulRetry(string retriedMessageUniqueId) =>
        throw new NotImplementedException();
}
