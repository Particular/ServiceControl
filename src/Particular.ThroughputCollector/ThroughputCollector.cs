namespace Particular.ThroughputCollector
{
    using System.Threading.Tasks;
    using AuditThroughput;
    using Contracts;
    using Particular.ThroughputCollector.Shared;
    using Persistence;
    using ServiceControl.Api;
    using ServiceControl.Transports;

    public class ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings, IConfigurationApi configurationApi, IThroughputQuery? throughputQuery = null)
        : IThroughputCollector
    {
        public async Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation()
        {
            var throughputConnectionSettings = new ThroughputConnectionSettings
            {
                Broker = throughputSettings.Broker,
                Settings = throughputSettings.Broker != Broker.None
                    ? throughputQuery?.Settings.Select(pair => new ThroughputConnectionSetting(pair.Key, pair.Description)).ToList() ?? []
                    : ServiceControlSettings.GetServiceControlConnectionSettings()
            };
            return await Task.FromResult(throughputConnectionSettings);
        }

        public async Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default)
        {
            var connectionTestResults = new ConnectionTestResults
            {
                Broker = throughputSettings.Broker,
                AuditConnectionResult = await AuditCommands.TestAuditConnection(configurationApi, cancellationToken)
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
                new Endpoint(e.Name, ThroughputSource.None)
                {
                    SanitizedName = e.Name,
                    UserIndicator = e.UserIndicator,
                }).ToList());

            await Task.CompletedTask;
        }

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary()
        {
            var endpoints = await dataStore.GetAllEndpoints(false);
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
                Broker = throughputSettings.Broker,
                ReportCanBeGenerated = await dataStore.IsThereThroughputForLastXDays(30)
            };

            return reportGenerationState;
        }

        public async Task<SignedReport> GenerateThroughputReport(string[]? masks, string? spVersion)
        {
            CreateMasks(masks);

            var endpoints = await dataStore.GetAllEndpoints(false);
            var endpointThroughputs = new List<Contracts.QueueThroughput>();
            List<string> ignoredQueueNames = [];

            //group endpoints by sanitized name - so to group throughput recorded from broker, audit and monitoring
            foreach (var endpoint in endpoints.GroupBy(g => g.SanitizedName))
            {
                //want to display the endpoint name if it's different to the sanitized endpoint name
                var queueName = endpoint.Any(w => w.Id.Name != w.SanitizedName) ? endpoint.First(w => w.Id.Name != w.SanitizedName).Id.Name : endpoint.Key;

                //get all data that we have, including daily values
                var endpointThroghput = new Contracts.QueueThroughput
                {
                    QueueName = Mask(queueName),
                    UserIndicator = UserIndicator(endpoint) ?? string.Empty,
                    EndpointIndicators = EndpointIndicators(endpoint) ?? [],
                    NoDataOrSendOnly = NoDataOrSendOnly(endpoint),
                    Scope = EndpointScope(endpoint) ?? "",
                    Throughput = MaxDailyThroughput(endpoint) ?? 0,
                    DailyThroughputFromAudit = AuditThroughput(endpoint) ?? [],
                    DailyThroughputFromMonitoring = MonitoringThroughput(endpoint) ?? [],
                    DailyThroughputFromBroker = BrokerThroughput(endpoint) ?? []
                };

                endpointThroughputs.Add(endpointThroghput);
            }

            var brokerData = await dataStore.GetBrokerData(throughputSettings.Broker);
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var report = new Report
            {
                EndTime = new DateTimeOffset(yesterday, TimeSpan.Zero),
                CustomerName = throughputSettings.CustomerName, //who the license is registeredTo
                ReportMethod = "NA",
                ScopeType = brokerData?.ScopeType ?? "",
                Prefix = null,
                MessageTransport = throughputQuery?.MessageTransport ?? throughputSettings.TransportType,
                ToolVersion = "V3", //ensure we check for this on the other side - ie that we can process V3
                ServiceControlVersion = throughputSettings.ServiceControlVersion,
                ServicePulseVersion = spVersion ?? "",
                IgnoredQueues = ignoredQueueNames?.ToArray() ?? [],
                Queues = endpointThroughputs.ToArray(),
                TotalQueues = endpointThroughputs.Count(),
                TotalThroughput = endpointThroughputs.Sum(q => q.Throughput ?? 0),
                EnvironmentData = brokerData?.Data ?? []
            };

            //this will be the date of the first throughput that we have received
            var firstAuditThroughputDate = endpointThroughputs.SelectMany(w => w.DailyThroughputFromAudit).MinBy(m => m.DateUTC)?.DateUTC.ToDateTime(TimeOnly.MinValue) ?? yesterday.AddDays(-1);
            var firstMonitoringThroughputDate = endpointThroughputs.SelectMany(w => w.DailyThroughputFromMonitoring).MinBy(m => m.DateUTC)?.DateUTC.ToDateTime(TimeOnly.MinValue) ?? yesterday.AddDays(-1);
            var firstBrokerThroughputDate = endpointThroughputs.SelectMany(w => w.DailyThroughputFromBroker).MinBy(m => m.DateUTC)?.DateUTC.ToDateTime(TimeOnly.MinValue) ?? yesterday.AddDays(-1);
            report.StartTime = new DateTimeOffset(new[] { firstAuditThroughputDate, firstMonitoringThroughputDate, firstBrokerThroughputDate }.Min(), TimeSpan.Zero);
            report.ReportDuration = report.EndTime - report.StartTime;

            report.EnvironmentData.Add(EnvironmentData.AuditEnabled.ToString(), endpoints.Any(a => a.Id.ThroughputSource == ThroughputSource.Audit && a.DailyThroughput?.Count > 0).ToString());
            report.EnvironmentData.Add(EnvironmentData.MonitoringEnabled.ToString(), endpoints.Any(a => a.Id.ThroughputSource == ThroughputSource.Monitoring && a.DailyThroughput?.Count > 0).ToString());

            var throughputReport = new SignedReport() { ReportData = report, Signature = Signature.SignReport(report) };
            return await Task.FromResult(throughputReport);
        }

        void CreateMasks(string[]? wordsToMask)
        {
            if (wordsToMask != null)
            {
                var number = 0;
                masks = wordsToMask
                    .Select(mask =>
                    {
                        number++;
                        return (mask, $"REDACTED{number}");
                    })
                    .ToArray();
            }
        }

        string Mask(string stringToMask)
        {
            foreach (var (mask, replacement) in masks)
            {
                stringToMask = stringToMask.Replace(mask, replacement, StringComparison.OrdinalIgnoreCase);
            }

            return stringToMask;
        }

        string? UserIndicator(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.UserIndicator))?.UserIndicator;

        string? EndpointScope(IGrouping<string, Endpoint> endpoint) => endpoint.FirstOrDefault(s => !string.IsNullOrEmpty(s.Scope))?.Scope;
        bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));
        //bool IsPlatformEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.PlatformEndpoint.ToString()));

        bool NoDataOrSendOnly(IGrouping<string, Endpoint> endpoint) => endpoint.All(a => a.DailyThroughput.Sum(s => s.TotalThroughput) == 0);
        string[]? EndpointIndicators(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.EndpointIndicators?.Any() == true)?.SelectMany(s => s.EndpointIndicators)?.Distinct()?.ToArray();
        EndpointDailyThroughput[]? AuditThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.Id.ThroughputSource == ThroughputSource.Audit)?.SelectMany(s => s.DailyThroughput)?.ToArray();
        EndpointDailyThroughput[]? MonitoringThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.Id.ThroughputSource == ThroughputSource.Monitoring)?.SelectMany(s => s.DailyThroughput)?.ToArray();
        EndpointDailyThroughput[]? BrokerThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.Id.ThroughputSource == ThroughputSource.Broker && (string.IsNullOrEmpty(w.UserIndicator) || !w.UserIndicator.Equals(Contracts.UserIndicator.NotNServicebusEndpoint.ToString(), StringComparison.OrdinalIgnoreCase)))?.SelectMany(s => s.DailyThroughput)?.ToArray();

        //get the max throughput recorded for the endpoints - shouldn't matter where it comes from (ie broker or service control)
        long? MaxDailyThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).MaxBy(m => m.TotalThroughput)?.TotalThroughput;

        //bool ThroughputExistsForThisPeriod(IGrouping<string, Endpoint> endpoint, int days) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).Any(m => m.DateUTC <= DateTime.UtcNow && m.DateUTC >= DateTime.UtcNow.AddDays(-days));

        (string Mask, string Replacement)[] masks = [];
    }
}
