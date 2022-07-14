namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;

    class InMemoryCustomCheckDataStore : ICustomChecksStorage
    {
        public Task UpdateCustomCheckStatus(CustomCheckDetail detail) => throw new System.NotImplementedException();
        public Task<QueryResult<IList<CustomCheck>>> GetStats(HttpRequestMessage request, string status = null) => throw new System.NotImplementedException();
    }
}