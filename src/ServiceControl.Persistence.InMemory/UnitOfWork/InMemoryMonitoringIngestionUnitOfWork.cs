namespace ServiceControl.Persistence.InMemory
{
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;

    class InMemoryMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        readonly InMemoryIngestionUnitOfWork parentUnitOfWork;

        public InMemoryMonitoringIngestionUnitOfWork(InMemoryIngestionUnitOfWork parentUnitOfWork)
            => this.parentUnitOfWork = parentUnitOfWork;

        public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
        {
            parentUnitOfWork.AddEndpoint(knownEndpoint);
            return Task.CompletedTask;
        }
    }
}