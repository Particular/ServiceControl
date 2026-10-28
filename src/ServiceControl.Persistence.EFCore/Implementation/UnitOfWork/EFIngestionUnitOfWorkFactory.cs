namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

public class EFIngestionUnitOfWorkFactory(
    IServiceProvider serviceProvider,
    MinimumRequiredStorageState storageState,
    IBodyStoragePersistence storagePersistence,
    IFailedMessageIngestionSqlDialect dialect,
    TimeProvider timeProvider) : IIngestionUnitOfWorkFactory
{
    public ValueTask<IIngestionUnitOfWork> StartNew(CancellationToken cancellationToken = default)
    {
        var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<EFPersisterSettings>();
        var unitOfWork = new EFIngestionUnitOfWork(scope, dbContext, storagePersistence, settings, dialect, timeProvider);
        return ValueTask.FromResult<IIngestionUnitOfWork>(unitOfWork);
    }

    public bool CanIngestMore() => storageState.CanIngestMore;

    // The batch writer is built for it: upserts guarded by the attempt times so the newer attempt
    // wins whichever transaction commits last, inserts that tolerate a competing writer's identical
    // row, and a consistent lock order. Running several ingestion hosts against one database
    // already relies on all of it.
    public bool SupportsConcurrentBatches => true;
}
