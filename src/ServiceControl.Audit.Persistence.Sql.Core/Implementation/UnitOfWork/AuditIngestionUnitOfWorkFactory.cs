namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation.UnitOfWork;

using Abstractions;
using DbContexts;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Persistence.UnitOfWork;

class AuditIngestionUnitOfWorkFactory(
    IServiceProvider serviceProvider,
    MinimumRequiredStorageState storageState,
    IBodyStoragePersistence storagePersistence,
    BatchIdGenerator batchIdGenerator)
    : IAuditIngestionUnitOfWorkFactory
{
    public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
    {
        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContextBase>();
        var settings = scope.ServiceProvider.GetRequiredService<AuditSqlPersisterSettings>();
        var unitOfWork = new AuditIngestionUnitOfWork(dbContext, storagePersistence, settings, batchIdGenerator);
        return ValueTask.FromResult<IAuditIngestionUnitOfWork>(unitOfWork);
    }

    public bool CanIngestMore() => storageState.CanIngestMore;
}
