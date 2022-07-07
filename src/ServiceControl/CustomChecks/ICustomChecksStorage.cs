namespace ServiceControl.CustomChecks
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;

    public interface ICustomChecksStorage
    {
        Task UpdateCustomCheckStatus(CustomCheckDetail detail);

        Task<StatisticsResult> GetStats(HttpRequestMessage request, string status = null);
    }
}