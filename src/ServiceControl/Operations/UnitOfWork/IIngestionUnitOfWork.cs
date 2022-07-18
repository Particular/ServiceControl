namespace ServiceControl.Operations
{
    using System.Threading.Tasks;

    interface IIngestionUnitOfWork
    {
        IMonitoringIngestionUnitOfWork Monitoring { get; }
        IRecoverabilityIngestionUnitOfWork Recoverability { get; }
        Task Complete();
    }
}