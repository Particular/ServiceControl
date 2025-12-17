namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System.Threading;
using System.Threading.Tasks;
using DbContexts;
using ServiceControl.Persistence;
using ServiceControl.Persistence.UnitOfWork;

class IngestionUnitOfWork : IngestionUnitOfWorkBase
{
    public IngestionUnitOfWork(ServiceControlDbContextBase dbContext, PersistenceSettings settings)
    {
        DbContext = dbContext;
        Settings = settings;
        Monitoring = new MonitoringIngestionUnitOfWork(this);
        Recoverability = new RecoverabilityIngestionUnitOfWork(this);
    }

    internal ServiceControlDbContextBase DbContext { get; }
    internal PersistenceSettings Settings { get; }

    // EF Core automatically batches all pending operations
    // The upsert operations execute SQL directly, but EF Core tracked changes (Add/Remove/Update) are batched
    public override Task Complete(CancellationToken cancellationToken) =>
        DbContext.SaveChangesAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DbContext?.Dispose();
        }
        base.Dispose(disposing);
    }
}

