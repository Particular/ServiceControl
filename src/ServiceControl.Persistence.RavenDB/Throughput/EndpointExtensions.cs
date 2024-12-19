#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput;

using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

static class EndpointExtensions
{
    public static string GenerateDocumentId(this EndpointIdentifier endpointId) => $"{endpointId.Name}/{endpointId.ThroughputSource}";

    public static string GenerateDocumentId(this EndpointDocument endpoint) => endpoint.EndpointId.GenerateDocumentId();

    public static Endpoint ToEndpoint(this EndpointDocument endpointDocument) => new(endpointDocument.EndpointId)
    {
        SanitizedName = endpointDocument.SanitizedName,
        EndpointIndicators = [.. endpointDocument.EndpointIndicators],
        UserIndicator = endpointDocument.UserIndicator,
        Scope = endpointDocument.Scope
    };

    public static EndpointDocument ToEndpointDocument(this Endpoint endpoint)
    {
        var document = new EndpointDocument(endpoint.Id)
        {
            SanitizedName = endpoint.SanitizedName,
            UserIndicator = endpoint.UserIndicator
        };

        foreach (var indicator in endpoint.EndpointIndicators ?? [])
        {
            document.EndpointIndicators.Add(indicator);
        }

        if (!string.IsNullOrEmpty(endpoint.Scope))
        {
            document.Scope = endpoint.Scope;
        }

        return document;
    }
}