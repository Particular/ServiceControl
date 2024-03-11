namespace Particular.ThroughputCollector
{
    using System.Linq;
    using System.Threading.Tasks;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.Shared;

    //#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public class ThroughputCollector : IThroughputCollector
    {
        public ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings)
        {
            this.dataStore = dataStore;
            this.throughputSettings = throughputSettings;
        }

        public async Task<BrokerSettings> GetBrokerSettingsInformation()
        {
            var brokerSettings = BrokerSettingsLibrary.Find(throughputSettings.Broker);

            brokerSettings ??= new BrokerSettings { Broker = throughputSettings.Broker };

            return await Task.FromResult(brokerSettings);
        }

        public async Task<BrokerSettingsTestResult> TestBrokerSettings()
        {
            //TODO
            return await Task.FromResult(new BrokerSettingsTestResult { Broker = throughputSettings.Broker, ConnectionSuccessful = true });
        }

        public async Task UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointThroughputs)
        {
            await dataStore.UpdateUserIndicationOnEndpoints(endpointThroughputs.Select(e =>
            {
                return new Endpoint
                {
                    Name = e.Name,
                    UserIndicatedSendOnly = e.UserIndicatedSendOnly,
                    UserIndicatedToIgnore = e.UserIndicatedToIgnore,
                };
            }).ToList());

            await Task.CompletedTask;
        }

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary(int? days)
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
                    UserIndicatedSendOnly = UserIndicatedSendOnly(endpoint),
                    UserIndicatedToIgnore = UserIndicatedToIgnore(endpoint),
                    IsKnownEndpoint = IsKnownEndpoint(endpoint),
                    MaxDailyThroughput = MaxDailyThroughput(endpoint) ?? 0,
                    ThroughputExistsForThisPeriod = ThroughputExistsForThisPeriod(endpoint, days ?? daysToReportOn)
                };

                endpointSummaries.Add(endpointSummary);
            }

            return await Task.FromResult(endpointSummaries);
        }

        public async Task<ReportGenerationState> GetReportGenerationState()
        {
            //TODO
            return await Task.FromResult(new ReportGenerationState { Broker = throughputSettings.Broker, ConnectionToBrokerWorking = true, EnoughDataToReportOn = true, ReportCanBeGenerated = true });
        }

        public async Task<SignedReport> GenerateThroughputReport(int? days, string? prefix, string[]? masks)
        {
            //TODO

            days ??= daysToReportOn;

            //get ones with prefix only

            //generate masks and mask the names

            //get all data that we have, including daily values

            var report = new Report
            {
                StartTime = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-(int)days), TimeSpan.Zero),
                EndTime = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero),
                ReportDuration = TimeSpan.FromDays(1),
                CustomerName = throughputSettings.CustomerName, //who the license is registeredTo
                ReportMethod = Mask("TODO"),
                ScopeType = "TODO",
                Prefix = prefix,
                MessageTransport = throughputSettings.Broker.ToString(),// "TODO",
                ToolVersion = "1",
            };



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

        bool UserIndicatedSendOnly(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.UserIndicatedSendOnly == true);
        bool UserIndicatedToIgnore(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.UserIndicatedToIgnore == true);
        bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));

        //get the max throughput recorded for the endpoints - shouldn't matter where it comes from (ie broker or service control)
        long? MaxDailyThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).MaxBy(m => m.TotalThroughput)?.TotalThroughput;
        bool ThroughputExistsForThisPeriod(IGrouping<string, Endpoint> endpoint, int days) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).Any(m => m.DateUTC <= DateTime.UtcNow && m.DateUTC >= DateTime.UtcNow.AddDays(-days));

        async Task<List<Endpoint>> GetRelevantEndpoints()
        {
            var endpoints = (List<Endpoint>)await dataStore.GetAllEndpoints();

            //remove error, audit and other platform queues from all
            return endpoints.Where(w => w.Name != throughputSettings.ErrorQueue && w.Name != throughputSettings.AuditQueue && w.Name != throughputSettings.ServiceControlQueue).ToList();
        }

        readonly IThroughputDataStore dataStore;
        readonly ThroughputSettings throughputSettings;
        readonly int daysToReportOn = 30;
        (string Mask, string Replacement)[] masks = [];
    }
}
