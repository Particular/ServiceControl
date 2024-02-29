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

        public async Task<List<EndpointThroughputSummary>> GetThroughputSummary()
        {
            //var endpoints = await SanitizeEndpoints(await dataStore.GetAllEndpoints().ToList().ConfigureAwait(false), throughputSettings.Broker).ConfigureAwait(false);

            //from all remove error and audit queues

            return await Task.FromResult<List<EndpointThroughputSummary>>([]).ConfigureAwait(false);
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

        async Task<List<Endpoint>> SanitizeEndpoints(List<Endpoint> endpoints, Contracts.Broker broker)
        {
            //if looking at broker transport - get all and mark as known if exists in audit or monitoring - also grab max throughput from all

            //if looking at non broker transport - get all from monitoring and audit - all marked as known - grab max throughput from all
        }

        readonly IThroughputDataStore dataStore;
        readonly ThroughputSettings throughputSettings;
    }
}
