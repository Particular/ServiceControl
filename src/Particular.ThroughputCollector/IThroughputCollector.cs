namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary(int? days);
        Task UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<BrokerSettings> GetBrokerSettingsInformation();
        Task<BrokerSettingsTestResult> TestBrokerSettings();
        Task<ThroughputReport> GenerateThroughputReport(int? days);
    }
}
