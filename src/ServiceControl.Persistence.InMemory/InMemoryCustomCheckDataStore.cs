namespace ServiceControl.Persistence.InMemory
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Infrastructure;
    using ServiceControl.CustomChecks;
    using ServiceControl.Persistence;

    class InMemoryCustomCheckDataStore : ICustomChecksDataStore
    {
        public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail) => throw new System.NotImplementedException();
        public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null) => throw new System.NotImplementedException();
        public Task Setup() => Task.CompletedTask;
    }
}