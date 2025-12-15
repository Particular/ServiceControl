namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;
using ServiceControl.Persistence.UnitOfWork;

class IngestionUnitOfWorkFactory(IServiceProvider serviceProvider, MinimumRequiredStorageState storageState) : IIngestionUnitOfWorkFactory
{
    public ValueTask<IIngestionUnitOfWork> StartNew()
    {
        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
        var unitOfWork = new IngestionUnitOfWork(dbContext);
        return ValueTask.FromResult<IIngestionUnitOfWork>(unitOfWork);
    }

    public bool CanIngestMore() => storageState.CanIngestMore;
}
