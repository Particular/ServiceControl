namespace Particular.ThroughputCollector
{
    using Particular.ThroughputCollector.Contracts;

    public interface IThroughputCollector
    {
        List<EndpointThroughputSummary> GetThroughputSummary();
    }
}
