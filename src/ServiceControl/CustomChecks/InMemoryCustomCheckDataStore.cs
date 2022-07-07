namespace ServiceControl.CustomChecks
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;

    public class InMemoryCustomCheckDataStore : ICustomChecksStorage
    {
        public Task UpdateCustomCheckStatus(CustomCheckDetail detail) => throw new System.NotImplementedException();
        public Task<StatisticsResult> GetStats(HttpRequestMessage request, string status = null) => throw new System.NotImplementedException();
    }
}