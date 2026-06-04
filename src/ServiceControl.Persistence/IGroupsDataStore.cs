namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    public interface IGroupsDataStore
    {
        Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter, AuthorizationInfo authInfo);
        Task<RetryBatch> GetCurrentForwardingBatch();
    }
}
