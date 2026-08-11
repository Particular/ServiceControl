namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class IngestionUnitOfWorkBase : IIngestionUnitOfWork
    {
        // Mirrors IAsyncDisposable.DisposeAsync, which declares no token, so one cannot be added here.
#pragma warning disable PS0018
        protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
#pragma warning restore PS0018

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; protected set; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; protected set; }
        public virtual Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
