namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;
using ServiceControl.Persistence.UnitOfWork;

class IngestionUnitOfWorkFactory(IServiceProvider serviceProvider, MinimumRequiredStorageState storageState, FileSystemBodyStorageHelper storageHelper) : IIngestionUnitOfWorkFactory
{
    public ValueTask<IIngestionUnitOfWork> StartNew()
    {
        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
        var settings = scope.ServiceProvider.GetRequiredService<PersistenceSettings>();
        var unitOfWork = new IngestionUnitOfWork(dbContext, storageHelper, settings, serviceProvider);
        return ValueTask.FromResult<IIngestionUnitOfWork>(unitOfWork);
    }

    public bool CanIngestMore() => storageState.CanIngestMore;
}
