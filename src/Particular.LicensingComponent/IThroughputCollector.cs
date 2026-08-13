namespace Particular.LicensingComponent
{
    using Contracts;
    using Particular.LicensingComponent.Report;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken = default);
        Task UpdateUserIndicatorsOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken = default);
        Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation(CancellationToken cancellationToken = default);
        Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default);
        Task<SignedReport> GenerateThroughputReport(string spVersion, DateTime? reportEndDate, CancellationToken cancellationToken = default);
        Task<ReportGenerationState> GetReportGenerationState(CancellationToken cancellationToken = default);
        Task<List<string>> GetReportMasks(CancellationToken cancellationToken = default);
        Task UpdateReportMasks(List<string> reportMaskUpdates, CancellationToken cancellationToken = default);
    }
}
