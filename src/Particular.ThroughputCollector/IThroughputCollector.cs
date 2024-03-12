namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary();
        Task UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<BrokerSettings> GetBrokerSettingsInformation();
        Task<BrokerSettingsTestResult> TestBrokerSettings();
        Task<SignedReport> GenerateThroughputReport(string? prefix, string[]? masks, string? spVersion);
        Task<ReportGenerationState> GetReportGenerationState();
    }
}
