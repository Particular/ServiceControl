namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary(int month);
        Task UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<BrokerSettings> GetBrokerSettings();
        Task<BrokerSettingsTestResult> TestBrokerSettings();
        Task<ThroughputReport> GenerateThroughputReport(int month);
    }
}
