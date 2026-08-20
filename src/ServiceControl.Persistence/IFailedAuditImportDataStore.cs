namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IFailedAuditImportDataStore
    {
        Task StoreFailedAuditImport(FailedAuditImport failure, CancellationToken cancellationToken = default);
        Task ProcessFailedAuditImports(Func<FailedTransportMessage, CancellationToken, Task> processMessage, CancellationToken cancellationToken = default);
        Task<bool> QueryContainsFailedImports(CancellationToken cancellationToken = default);
    }
}
