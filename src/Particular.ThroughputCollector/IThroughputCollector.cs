namespace Particular.ThroughputCollector
{
    using Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken = default);
        Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation();
        Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default);
        Task<SignedReport> GenerateThroughputReport(string[] masks, string spVersion, CancellationToken cancellationToken = default);
        Task<ReportGenerationState> GetReportGenerationState();
    }
}
