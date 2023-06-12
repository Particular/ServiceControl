namespace ServiceControl.Audit.Persistence.InMemory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    class InMemoryPersistenceLifecycle : IPersistenceLifecycle
    {
        public Task Start(Action onCriticalError, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        public Task Stop(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}