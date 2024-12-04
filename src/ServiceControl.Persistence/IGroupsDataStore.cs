namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Recoverability;

    public interface IGroupsDataStore
    {
        Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter);
        Task<RetryBatch> GetCurrentForwardingBatch();
    }
}