namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary();
        Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation();
        Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default);
        Task<SignedReport> GenerateThroughputReport(string? prefix, string[]? masks, string? spVersion);
        Task<ReportGenerationState> GetReportGenerationState();
    }
}
