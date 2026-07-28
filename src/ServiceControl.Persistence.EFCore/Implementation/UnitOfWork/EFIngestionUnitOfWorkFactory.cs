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
    IIngestionSqlDialect dialect,
    TimeProvider timeProvider) : IIngestionUnitOfWorkFactory
{
    public ValueTask<IIngestionUnitOfWork> StartNew()
    {
        var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<EFPersisterSettings>();
        var unitOfWork = new EFIngestionUnitOfWork(scope, dbContext, storagePersistence, settings, dialect, timeProvider);
        return ValueTask.FromResult<IIngestionUnitOfWork>(unitOfWork);
    }

    public bool CanIngestMore() => storageState.CanIngestMore;
}
