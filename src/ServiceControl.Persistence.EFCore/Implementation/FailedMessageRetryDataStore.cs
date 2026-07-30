namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;

public class FailedMessageRetryDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageRetryDataStore
{
    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback) =>
        throw new NotImplementedException();

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress) =>
        throw new NotImplementedException();

    public Task RemoveFailedMessageRetry(string uniqueMessageId) =>
        throw new NotImplementedException();

    public Task<byte[]> GetFailedMessageBody(string uniqueMessageId) =>
        throw new NotImplementedException();
}
