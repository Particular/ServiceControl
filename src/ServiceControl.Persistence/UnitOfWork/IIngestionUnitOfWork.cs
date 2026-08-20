namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IIngestionUnitOfWork : IAsyncDisposable
    {
        IMonitoringIngestionUnitOfWork? Monitoring { get; }
        IRecoverabilityIngestionUnitOfWork? Recoverability { get; }

        /// <summary>
        /// Null unless the persister advertises SupportsAuditIngestion in its manifest.
        /// </summary>
        IAuditIngestionUnitOfWork? Audit { get; }

        Task Complete(CancellationToken cancellationToken = default);
    }
}
