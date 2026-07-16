namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Recoverability;

public class GroupsDataStore : IGroupsDataStore
{
    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter) =>
        throw new NotImplementedException();

    public Task<RetryBatch> GetCurrentForwardingBatch() =>
        throw new NotImplementedException();
}
