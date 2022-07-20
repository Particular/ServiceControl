namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using Monitoring;

    interface IMonitoringIngestionUnitOfWork
    {
        Task RecordKnownEndpoint(KnownEndpoint knownEndpoint);
    }
}