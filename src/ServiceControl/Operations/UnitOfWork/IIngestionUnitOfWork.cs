namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;

    interface IIngestionUnitOfWork : IDisposable
    {
        IMonitoringIngestionUnitOfWork Monitoring { get; }
        IRecoverabilityIngestionUnitOfWork Recoverability { get; }
        Task Complete();
    }
}