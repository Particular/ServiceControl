namespace Particular.LicensingComponent
{
    using Contracts;
    using Particular.LicensingComponent.Report;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken);
        Task UpdateUserIndicatorsOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken);
        Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation(CancellationToken cancellationToken);
        Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken);
        Task<SignedReport> GenerateThroughputReport(string spVersion, DateTime? reportEndDate, CancellationToken cancellationToken);
        Task<ReportGenerationState> GetReportGenerationState(CancellationToken cancellationToken);
        Task<List<string>> GetReportMasks(CancellationToken cancellationToken);
        Task UpdateReportMasks(List<string> reportMaskUpdates, CancellationToken cancellationToken);
    }
}