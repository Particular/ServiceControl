namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Threading;
    using System.Threading.Tasks;

    public class InMemoryPersistenceInstaller : IPersistenceInstaller
    {
        public Task Install(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}