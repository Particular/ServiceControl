namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using System.Threading.Tasks;

    public interface IIngestionUnitOfWork : IDisposable
    {
        IMonitoringIngestionUnitOfWork Monitoring { get; }
        IRecoverabilityIngestionUnitOfWork Recoverability { get; }
        Task Complete();
    }
}