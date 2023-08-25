namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IFailedErrorImportDataStore
    {
        Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage, CancellationToken cancellationToken);
        Task<bool> QueryContainsFailedImports();
    }
}