namespace Particular.ThroughputCollector
{
    using System.Linq;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;

    public class ThroughputCollector : IThroughputCollector
    {
        public ThroughputCollector(IThroughputDataStore dataStore, ThroughputSettings throughputSettings)
        {
            this.dataStore = dataStore;
            this.throughputSettings = throughputSettings;
        }

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary(int month)
        {
            var endpoints = await SanitizeEndpoints((List<Endpoint>)await dataStore.GetAllEndpoints().ConfigureAwait(false), month).ConfigureAwait(false);

            //remove error and audit queues from all
            endpoints = endpoints.Where(w => w.Name != throughputSettings.ErrorQueue && w.Name != throughputSettings.AuditQueue).ToList();

            var endpointSummary = new List<EndpointThroughputSummary>();
            endpoints.ForEach(e =>
            {
                endpointSummary.Add(new EndpointThroughputSummary
                {
                    Name = e.Name,
                    Queue = e.Queue,
                    ThroughputSource = e.ThroughputSource.ToString(),
                    IsKnownEndpoint = e.EndpointIndicators.Any(),
                    UserIndicatedSendOnly = e.UserIndicatedSendOnly,
                    UserIndicatedToIgnore = e.UserIndicatedToIgnore,
                    MaxDailyThroughputForThisMonth = e.DailyThroughput.MaxBy(m => m.TotalThroughput)?.TotalThroughput
                });
            });

            return await Task.FromResult(endpointSummary).ConfigureAwait(false);
        }

        public async Task UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointThroughputs)
        {
            await dataStore.UpdateUserIndicationOnEndpoints(endpointThroughputs.Select(e =>
            {
                return new Endpoint
                {
                    Name = e.Name,
                    Queue = e.Queue,
                    ThroughputSource = Enum.TryParse(e.ThroughputSource, true, out ThroughputSource throughputSource) ? throughputSource : ThroughputSource.Broker,
                    UserIndicatedSendOnly = e.UserIndicatedSendOnly,
                    UserIndicatedToIgnore = e.UserIndicatedToIgnore
                };
            }).ToList()).ConfigureAwait(false);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<ThroughputReport> GenerateThroughputReport(int month)
        {
            //TODO

            var throughputReport = new ThroughputReport();
            return await Task.FromResult(throughputReport).ConfigureAwait(false);
        }

        public async Task<BrokerSettings> GetBrokerSettings()
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

        async Task<List<Endpoint>> SanitizeEndpoints(List<Endpoint> endpoints, int month)
        {
            //get endpoints that have recorded throughput for this month
            endpoints = endpoints.Where(w => w.DailyThroughput.Any(a => a.DateUTC.Month == month)).ToList();

            if (throughputSettings.Broker == Contracts.Broker.None)
            {
                //if looking at non broker transport - get all from monitoring and audit - all already marked as known - grab max throughput from all
                var saitizedEndpoints = endpoints.Where(w => w.ThroughputSource != ThroughputSource.Broker).GroupBy(g => g.Name).ToList();
            }
            else
            {
                //if looking at broker transport - get all and mark as known if exists in audit or monitoring - also grab max throughput from all

            }

            //TODO - do we only grab endpoints that have a recorded throughput for "this month"? or "last 30 days"?

            return await Task.FromResult(endpoints).ConfigureAwait(false);
        }

        readonly IThroughputDataStore dataStore;
        readonly ThroughputSettings throughputSettings;
    }
}
