namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

public class EFIngestionUnitOfWork : IIngestionUnitOfWork
{
    readonly ServiceControlDbContext dbContext;
    readonly IAsyncDisposable scope;

    public EFIngestionUnitOfWork(IAsyncDisposable scope, ServiceControlDbContext dbContext, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings)
    {
        this.scope = scope;
        this.dbContext = dbContext;
        Recoverability = new EFRecoverabilityIngestionUnitOfWork(dbContext, storagePersistence, settings);
        Monitoring = new EFMonitoringIngestionUnitOfWork(dbContext);
    }

    public IMonitoringIngestionUnitOfWork Monitoring { get; }

    public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

    public Task Complete(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await scope.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}

