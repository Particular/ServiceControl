namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class IngestionUnitOfWorkBase : IIngestionUnitOfWork
    {
        protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; protected set; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; protected set; }
        public virtual Task Complete(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
