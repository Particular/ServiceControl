namespace ServiceControl.Operations
{
    using Monitoring;

    class InMemoryMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        readonly InMemoryIngestionUnitOfWork parentUnitOfWork;

        public InMemoryMonitoringIngestionUnitOfWork(InMemoryIngestionUnitOfWork parentUnitOfWork)
            => this.parentUnitOfWork = parentUnitOfWork;

        public void RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => parentUnitOfWork.AddEndpoint(knownEndpoint);
    }
}