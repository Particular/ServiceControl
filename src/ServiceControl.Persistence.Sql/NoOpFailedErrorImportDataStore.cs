namespace ServiceControl.Persistence.Sql;

using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class NoOpFailedErrorImportDataStore : IFailedErrorImportDataStore
{
    public Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> QueryContainsFailedImports() => Task.FromResult(false);
}
