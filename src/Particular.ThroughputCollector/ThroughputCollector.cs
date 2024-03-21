namespace Particular.ThroughputCollector
{
    using System.Linq;
    using System.Threading.Tasks;
    using Particular.ThroughputCollector.AuditThroughput;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Infrastructure;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.Shared;
    using ServiceControl.Api;

    public class ThroughputCollector : IThroughputCollector
    {
        public ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings, IConfigurationApi configurationApi)
        {
            this.dataStore = dataStore;
            this.throughputSettings = throughputSettings;
            this.configurationApi = configurationApi;
        }

        public async Task<BrokerSettings> GetBrokerSettingsInformation()
        {
            var brokerSettings = BrokerSettingsLibrary.Find(throughputSettings.Broker);

            brokerSettings ??= new BrokerSettings { Broker = throughputSettings.Broker };

            return await Task.FromResult(brokerSettings);
        }

        public async Task<ConnectionTestResults> TestConnectionSettings()
        {
            var connectionTestResults = new ConnectionTestResults
            {
                Broker = throughputSettings.Broker,
                AuditConnectionResult = await AuditCommands.TestAuditConnection(configurationApi),
                //TODO 1
                //MonitoringConnectionResult = ??
                //TODO 2
                //BrokerConnectionResult = ??;
            };

            return await Task.FromResult(connectionTestResults);
        }

        public async Task UpdateUserIndicatorsOnEndpoints(List<EndpointThroughputSummary> endpointThroughputs)
        {
            await dataStore.UpdateUserIndicatorOnEndpoints(endpointThroughputs.Select(e =>
            {
                return new Endpoint
                {
                    Name = e.Name,
                    SanitizedName = e.Name,
                    UserIndicator = e.UserIndicator,
                };
            }).ToList());

            await Task.CompletedTask;
        }

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary()
        {
            var endpoints = await GetRelevantEndpoints();

            var endpointSummaries = new List<EndpointThroughputSummary>();

            //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
            foreach (var endpoint in endpoints.GroupBy(g => g.SanitizedName))
            {
                var endpointSummary = new EndpointThroughputSummary
                {
                    //want to display the endpoint name to the user if it's different to the sanitized endpoint name
                    Name = endpoint.Any(w => w.Name != w.SanitizedName) ? endpoint.First(w => w.Name != w.SanitizedName).Name : endpoint.Key,
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
                Broker = throughputSettings.Broker,
                ReportCanBeGenerated = await dataStore.IsThereThroughputForLastXDays(30),
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
                CustomerName = throughputSettings.CustomerName, //who the license is registeredTo
                ReportMethod = Mask("TODO"),
                ScopeType = "TODO",
                Prefix = prefix,
                MessageTransport = throughputSettings.Broker.ToString(),// "TODO",
                ToolVersion = "1",
                ServiceControlVersion = throughputSettings.ServiceControlVersion
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

        async Task<List<Endpoint>> GetRelevantEndpoints()
        {
            var endpoints = (List<Endpoint>)await dataStore.GetAllEndpoints();

            //remove error, audit and other platform queues from all
            return endpoints.Where(w => w.Name != throughputSettings.ErrorQueue && w.Name != throughputSettings.AuditQueue && w.Name != throughputSettings.ServiceControlQueue).ToList();
        }

        readonly IThroughputDataStore dataStore;
        readonly ThroughputSettings throughputSettings;
        readonly IConfigurationApi configurationApi;
        (string Mask, string Replacement)[] masks = [];
    }
}
