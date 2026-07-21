namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

public class EFIngestionUnitOfWork : IIngestionUnitOfWork
{
    readonly ServiceControlDbContext dbContext;
    readonly AsyncServiceScope scope;

    public EFIngestionUnitOfWork(AsyncServiceScope scope, ServiceControlDbContext dbContext, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings)
    {
        this.scope = scope;
        this.dbContext = dbContext;
    }

    public IMonitoringIngestionUnitOfWork Monitoring =>
        throw new NotImplementedException();

    public IRecoverabilityIngestionUnitOfWork Recoverability =>
        throw new NotImplementedException();

    public Task Complete(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await scope.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
