namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Operations;

public class FailedErrorImportDataStore : IFailedErrorImportDataStore
{
    public Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<bool> QueryContainsFailedImports() =>
        throw new NotImplementedException();
}
