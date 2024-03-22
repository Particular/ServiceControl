namespace Particular.ThroughputCollector
{
    using AuditThroughput;
    using Contracts;
    using Infrastructure;
    using Persistence;
    using ServiceControl.Api;
    using ServiceControl.Transports;

    public class ThroughputCollector : IThroughputCollector
    {
        public ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings, IConfigurationApi configurationApi, IThroughputQuery? throughputQuery = null)
        {
            dataStore1 = dataStore;
            throughputSettings1 = throughputSettings;
            configurationApi1 = configurationApi;
            throughputQuery1 = throughputQuery;
        }

        public async Task<BrokerSettings> GetBrokerSettingsInformation()
        {
            var brokerSettings = new BrokerSettings { Broker = throughputSettings1.Broker, Settings = throughputQuery1?.Settings.Select(pair => new BrokerSetting(pair.Key, pair.Description)).ToList() ?? [] };
            return await Task.FromResult(brokerSettings);
        }

        public async Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default)
        {
            var connectionTestResults = new ConnectionTestResults
            {
                Broker = throughputSettings1.Broker,
                AuditConnectionResult = await AuditCommands.TestAuditConnection(configurationApi1, cancellationToken),
                //TODO 1
                //MonitoringConnectionResult = ??
                //TODO 2
                //BrokerConnectionResult = ??;
            };

            return await Task.FromResult(connectionTestResults);
        }

        public async Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointThroughputs)
        {
            await dataStore1.UpdateUserIndicatorOnEndpoints(endpointThroughputs.Select(e =>
                new Endpoint(e.Name, ThroughputSource.None)
                {
                    SanitizedName = e.Name,
                    UserIndicator = e.UserIndicator,
                }).ToList());

            await Task.CompletedTask;
        }

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary()
        {
            var endpoints = await dataStore1.GetAllEndpoints(includePlatformEndpoints: false);
            var endpointSummaries = new List<EndpointThroughputSummary>();

            //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
            foreach (var endpoint in endpoints.GroupBy(g => g.SanitizedName))
            {
                var endpointSummary = new EndpointThroughputSummary
                {
                    //want to display the endpoint name to the user if it's different to the sanitized endpoint name
                    Name = endpoint.Any(w => w.Id.Name != w.SanitizedName) ? endpoint.First(w => w.Id.Name != w.SanitizedName).Id.Name : endpoint.Key,
                    UserIndicator = UserIndicator(endpoint) ?? string.Empty,
                    IsKnownEndpoint = IsKnownEndpoint(endpoint),
                    MaxDailyThroughput = MaxDailyThroughput(endpoint) ?? 0
                };

                endpointSummaries.Add(endpointSummary);
            }

            return await Task.FromResult(endpointSummaries);
        }

        public async Task<ReportGenerationState> GetReportGenerationState()
        {
            var reportGenerationState = new ReportGenerationState
            {
                Broker = throughputSettings1.Broker,
                ReportCanBeGenerated = await dataStore1.IsThereThroughputForLastXDays(30),
            };

            return reportGenerationState;
        }

        public async Task<SignedReport> GenerateThroughputReport(string? prefix, string[]? masks, string? spVersion)
        {
            //TODO

            //get ones with prefix only - add ones without the prefix into IgnoredQueues

            //generate masks and mask the names

            //get all data that we have, including daily values

            var report = new Report
            {
                EndTime = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1), TimeSpan.Zero),
                ReportDuration = TimeSpan.FromDays(1),
                CustomerName = throughputSettings1.CustomerName, //who the license is registeredTo
                ReportMethod = Mask("TODO"),
                ScopeType = "TODO",
                Prefix = prefix,
                MessageTransport = throughputSettings1.Broker.ToString(),// "TODO",
                ToolVersion = "1",
                ServiceControlVersion = throughputSettings1.ServiceControlVersion
            };

            //TODO this will be the date of the first throughput that we have received
            //report.StartTime = 

            var throughputReport = new SignedReport() { ReportData = report, Signature = GetSignature() };
            return await Task.FromResult(throughputReport);
        }


        string Mask(string stringToMask)
        {
            foreach (var (mask, replacement) in masks)
            {
                stringToMask = stringToMask.Replace(mask, replacement, StringComparison.OrdinalIgnoreCase);
            }

            return stringToMask;
        }

        string GetSignature()
        {
            return string.Empty; //TODO
        }

        string? UserIndicator(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => string.IsNullOrEmpty(s.UserIndicator))?.UserIndicator;

        bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));

        //get the max throughput recorded for the endpoints - shouldn't matter where it comes from (ie broker or service control)

        long? MaxDailyThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).MaxBy(m => m.TotalThroughput)?.TotalThroughput;

        //bool ThroughputExistsForThisPeriod(IGrouping<string, Endpoint> endpoint, int days) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).Any(m => m.DateUTC <= DateTime.UtcNow && m.DateUTC >= DateTime.UtcNow.AddDays(-days));


        (string Mask, string Replacement)[] masks = [];

        private readonly IThroughputDataStore dataStore1;

        private readonly ThroughputSettings throughputSettings1;

        private readonly IConfigurationApi configurationApi1;

        private readonly IThroughputQuery? throughputQuery1;
    }
}
