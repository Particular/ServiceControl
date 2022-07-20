namespace ServiceControl.Persistence.SqlServer
{
    using System.Threading.Tasks;
    using Monitoring;
    using Operations;

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