namespace ServiceControl.Operations
{
    using Monitoring;

    class SqlMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
    {
        readonly SqlIngestionUnitOfWork parentUnitOfWork;

        public SqlMonitoringIngestionUnitOfWork(SqlIngestionUnitOfWork parentUnitOfWork)
            => this.parentUnitOfWork = parentUnitOfWork;

        public void RecordKnownEndpoint(KnownEndpoint knownEndpoint)
            => parentUnitOfWork.AddEndpoint(knownEndpoint);
    }
}