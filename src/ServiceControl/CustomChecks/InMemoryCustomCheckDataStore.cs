namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Infrastructure;

    class InMemoryCustomCheckDataStore : ICustomChecksDataStore
    {
        public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail) => throw new System.NotImplementedException();
        public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null) => throw new System.NotImplementedException();
    }
}