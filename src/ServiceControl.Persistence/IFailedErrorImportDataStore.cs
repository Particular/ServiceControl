namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IFailedErrorImportDataStore
    {
        Task StoreFailedErrorImport(FailedErrorImport failure, CancellationToken cancellationToken = default);
        Task ProcessFailedErrorImports(Func<FailedTransportMessage, CancellationToken, Task> processMessage, CancellationToken cancellationToken = default);
        Task<bool> QueryContainsFailedImports(CancellationToken cancellationToken = default);
    }
}