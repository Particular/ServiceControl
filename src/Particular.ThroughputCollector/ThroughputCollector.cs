namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;

    public class ThroughputCollector : IThroughputCollector
    {
        public ThroughputCollector(IThroughputDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        public List<EndpointThroughputSummary> GetThroughputSummary()
        {
            return [];
        }

        readonly IThroughputDataStore dataStore;
    }
}
