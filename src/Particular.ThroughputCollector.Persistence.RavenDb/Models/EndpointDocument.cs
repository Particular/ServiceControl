namespace Particular.ThroughputCollector.Persistence.RavenDb.Models;

using Particular.ThroughputCollector.Contracts;

class EndpointDocument(EndpointIdentifier id)
{
    public EndpointIdentifier EndpointId { get; } = id;
}
