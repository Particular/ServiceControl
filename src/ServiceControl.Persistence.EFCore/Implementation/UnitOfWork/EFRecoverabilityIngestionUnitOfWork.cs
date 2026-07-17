namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using NServiceBus.Transport;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.UnitOfWork;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.Persistence.EFCore.Abstractions;

#pragma warning disable CS9113 // Parameter is unread.
public class EFRecoverabilityIngestionUnitOfWork(ServiceControlDbContext dbContext, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings) : IRecoverabilityIngestionUnitOfWork
#pragma warning restore CS9113 // Parameter is unread.
{
    public Task RecordFailedProcessingAttempt(MessageContext context,
        FailedMessage.ProcessingAttempt processingAttempt,
        List<FailedMessage.FailureGroup> groups) =>
        throw new NotImplementedException();

    public Task RecordSuccessfulRetry(string retriedMessageUniqueId) =>
        throw new NotImplementedException();
}
