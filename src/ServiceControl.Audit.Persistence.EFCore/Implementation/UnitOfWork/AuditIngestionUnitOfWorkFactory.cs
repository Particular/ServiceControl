namespace ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.Audit.Persistence.UnitOfWork;

sealed class AuditIngestionUnitOfWorkFactory(IServiceScopeFactory scopeFactory, EFPersisterSettings settings, TimeProvider timeProvider) : IAuditIngestionUnitOfWorkFactory
{
    // Fixed for the whole batch, so the body ids it records match the rows it writes.
    public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IAuditIngestionUnitOfWork>(
            new AuditIngestionUnitOfWork(scopeFactory, settings, AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime)));

    public bool CanIngestMore() => true;

    public bool SupportsConcurrentBatches => true;
}
