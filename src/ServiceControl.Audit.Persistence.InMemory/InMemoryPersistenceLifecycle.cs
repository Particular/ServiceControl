namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    class InMemoryPersistenceLifecycle : IPersistenceLifecycle
    {
        public Task Start(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        public Task Stop(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}