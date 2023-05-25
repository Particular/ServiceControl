namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Threading.Tasks;

    public interface IMonitoringIngestionUnitOfWork
    {
        Task RecordKnownEndpoint(KnownEndpoint knownEndpoint);
    }
}