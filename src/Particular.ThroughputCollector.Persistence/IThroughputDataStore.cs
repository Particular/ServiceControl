namespace Particular.ThroughputCollector.Persistence;

public interface IThroughputDataStore
{
    Task<IReadOnlyList<Endpoint>> GetAllEndpoints();
    //Task<IReadOnlyList<Endpoint>> GetAllBrokerEndpoints();
    //Task<IReadOnlyList<Endpoint>> GetAllNonBrokerEndpoints();
    Task<Endpoint?> GetEndpointByName(string name, ThroughputSource throughputSource);
    Task RecordEndpointThroughput(Endpoint endpoint);
    Task UpdateUserIndicationOnEndpoints(List<Endpoint> endpointsWithUserIndicator);
    Task AppendEndpointThroughput(Endpoint endpoint);
}