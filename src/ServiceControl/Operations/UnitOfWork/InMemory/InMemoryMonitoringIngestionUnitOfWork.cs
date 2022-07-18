namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using Monitoring;

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