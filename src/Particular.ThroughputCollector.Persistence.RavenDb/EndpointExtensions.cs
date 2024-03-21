namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.Persistence.RavenDb.Models;

static class EndpointExtensions
{
    public static string GenerateDocumentId(this EndpointIdentifier endpointId) => $"{endpointId.Name}/{endpointId.ThroughputSource}";

    public static string GenerateDocumentId(this EndpointDocument endpoint) => endpoint.EndpointId.GenerateDocumentId();

    public static Endpoint ToEndpoint(this EndpointDocument endpointDocument) => new(endpointDocument.EndpointId);
}
