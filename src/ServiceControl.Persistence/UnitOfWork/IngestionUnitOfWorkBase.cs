namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class IngestionUnitOfWorkBase : IIngestionUnitOfWork
    {
        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~IngestionUnitOfWorkBase() => Dispose(false);

        public IMonitoringIngestionUnitOfWork Monitoring { get; protected set; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; protected set; }
        public virtual Task Complete(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}