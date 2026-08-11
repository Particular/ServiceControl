namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;

public class FailedMessageRetryDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageRetryDataStore
{
    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, CancellationToken, Task> processCallback, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task RemoveFailedMessageRetry(string uniqueMessageId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<byte[]> GetFailedMessageBody(string uniqueMessageId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
