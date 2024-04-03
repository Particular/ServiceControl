namespace Particular.ThroughputCollector
{
    using Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary();
        Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation();
        Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default);
        Task<SignedReport> GenerateThroughputReport(string[] masks, string spVersion);
        Task<ReportGenerationState> GetReportGenerationState();
    }
}
