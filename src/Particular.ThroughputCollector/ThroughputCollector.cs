namespace Particular.ThroughputCollector
{
    using System.Linq;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.Shared;

    public class ThroughputCollector : IThroughputCollector
    {
        public ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings)
        {
            this.dataStore = dataStore;
            this.throughputSettings = throughputSettings;
        }

        bool UserIndicatedSendOnly(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.UserIndicatedSendOnly == true);
        bool UserIndicatedToIgnore(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.UserIndicatedToIgnore == true);
        bool IsKnownEndpoint(IGrouping<string, Endpoint> endpoint) => endpoint.Any(s => s.EndpointIndicators != null && s.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()));

        //get the max throughput recorded for the endpoints - shouldn't matter where it comes from (ie broker or service control)
        long? MaxDailyThroughput(IGrouping<string, Endpoint> endpoint) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).MaxBy(m => m.TotalThroughput)?.TotalThroughput;
        bool ThroughputExistsForThisPeriod(IGrouping<string, Endpoint> endpoint, int days) => endpoint.Where(w => w.DailyThroughput != null).SelectMany(s => s.DailyThroughput).Any(m => m.DateUTC <= DateTime.UtcNow && m.DateUTC >= DateTime.UtcNow.AddDays(-days));

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary(int? days)
        {
            var endpoints = await GetRelevantEndpoints().ConfigureAwait(false);

            var endpointSummaries = new List<EndpointThroughputSummary>();

            //group endpoints by name - so to group throughput recorded from broker, audit and monitoring
            foreach (var endpoint in endpoints.GroupBy(g => g.Name))
            {
                var endpointSummary = new EndpointThroughputSummary
                {
                    Name = endpoint.Key,
                    UserIndicatedSendOnly = UserIndicatedSendOnly(endpoint),
                    UserIndicatedToIgnore = UserIndicatedToIgnore(endpoint),
                    IsKnownEndpoint = IsKnownEndpoint(endpoint),
                    MaxDailyThroughput = MaxDailyThroughput(endpoint) ?? 0,
                    ThroughputExistsForThisPeriod = ThroughputExistsForThisPeriod(endpoint, days ?? daysToReportOn)
                };

                endpointSummaries.Add(endpointSummary);
            }

            return await Task.FromResult(endpointSummaries).ConfigureAwait(false);
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
            }).ToList()).ConfigureAwait(false);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<ThroughputReport> GenerateThroughputReport(int? days)
        {
            //TODO

            var throughputReport = new ThroughputReport();
            return await Task.FromResult(throughputReport).ConfigureAwait(false);
        }

        public async Task<BrokerSettings> GetBrokerSettingsInformation()
        {
            var brokerSettings = BrokerManifestLibrary.Find(throughputSettings.Broker);

            brokerSettings ??= new BrokerSettings { Broker = throughputSettings.Broker };

            return await Task.FromResult(brokerSettings).ConfigureAwait(false);
        }

        public async Task<BrokerSettingsTestResult> TestBrokerSettings()
        {
            //TODO
            return await Task.FromResult(new BrokerSettingsTestResult { Broker = throughputSettings.Broker, ConnectionSuccessful = true }).ConfigureAwait(false);
        }

        async Task<List<Endpoint>> GetRelevantEndpoints()
        {
            var endpoints = (List<Endpoint>)await dataStore.GetAllEndpoints().ConfigureAwait(false);

            //remove error, audit and other platform queues from all
            return endpoints.Where(w => w.Name != throughputSettings.ErrorQueue && w.Name != throughputSettings.AuditQueue && w.Name != throughputSettings.ServiceControlQueue).ToList();
        }

        readonly IThroughputDataStore dataStore;
        readonly ThroughputSettings throughputSettings;
        readonly int daysToReportOn = 30;
    }
}
