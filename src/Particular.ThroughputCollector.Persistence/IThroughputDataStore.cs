namespace Particular.ThroughputCollector.Persistence;

public interface IThroughputDataStore
{
    Task<IReadOnlyList<Endpoint>> GetAllEndpoints();
    //Task<IReadOnlyList<Endpoint>> GetAllBrokerEndpoints();
    //Task<IReadOnlyList<Endpoint>> GetAllNonBrokerEndpoints();
    Task<Endpoint?> GetEndpointByName(string name, ThroughputSource throughputSource);
    Task RecordEndpointThroughput(Endpoint endpoint);
    Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator);
    Task AppendEndpointThroughput(Endpoint endpoint);
    Task<bool> IsThereThroughputForLastXDays(int days);
    Task<BrokerData?> GetBrokerData(Broker broker);
    Task SaveBrokerData(Broker broker, string? scopeType, string? Version);
}