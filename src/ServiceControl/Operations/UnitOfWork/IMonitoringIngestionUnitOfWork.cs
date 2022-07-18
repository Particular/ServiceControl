namespace ServiceControl.Operations
{
    using Monitoring;

    interface IMonitoringIngestionUnitOfWork
    {
        void RecordKnownEndpoint(KnownEndpoint knownEndpoint);
    }
}