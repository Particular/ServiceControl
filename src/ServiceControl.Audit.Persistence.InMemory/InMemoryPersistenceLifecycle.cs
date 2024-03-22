namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    class InMemoryPersistenceLifecycle : IPersistenceLifecycle
    {
        public Task Initialize(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}