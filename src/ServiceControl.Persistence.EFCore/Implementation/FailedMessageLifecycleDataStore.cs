namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Every operation here has to update both StatusChangedAt and LastModified.
/// </summary>
public class FailedMessageLifecycleDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IFailedMessageLifecycleDataStore
{
    public Task MarkAsArchived(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<bool> MarkAsResolved(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds) =>
        throw new NotImplementedException();

    public Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to) =>
        throw new NotImplementedException();

    public Task RevertRetry(string messageUniqueId) =>
        throw new NotImplementedException();
}
