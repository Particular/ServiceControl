namespace ServiceControl.Persistence.Sql;

using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;

class NoOpGroupsDataStore : IGroupsDataStore
{
    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter) =>
        Task.FromResult<IList<FailureGroupView>>([]);

    public Task<RetryBatch> GetCurrentForwardingBatch() =>
        Task.FromResult<RetryBatch>(null);
}
