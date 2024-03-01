namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        Task<List<EndpointThroughputSummary>> GetThroughputSummary();
        Task UpdateUserSelectionOnEndpointThroughput(List<EndpointThroughputSummary> endpointsThroughputSummary);
        Task<BrokerSettings> GetBrokerSettings();
    }
}
