namespace Particular.ThroughputCollector
{
    using Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken);
        Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointsThroughputSummary, CancellationToken cancellationToken);
        Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation(CancellationToken cancellationToken);
        Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken);
        Task<SignedReport> GenerateThroughputReport(string[] masks, string spVersion, CancellationToken cancellationToken);
        Task<ReportGenerationState> GetReportGenerationState(CancellationToken cancellationToken);
    }
}
