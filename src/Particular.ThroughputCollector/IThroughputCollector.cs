﻿namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary();
        Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<BrokerSettings> GetBrokerSettingsInformation();
        Task<ConnectionTestResults> TestConnectionSettings();
        Task<SignedReport> GenerateThroughputReport(string? prefix, string[]? masks, string? spVersion);
        Task<ReportGenerationState> GetReportGenerationState();
    }
}
