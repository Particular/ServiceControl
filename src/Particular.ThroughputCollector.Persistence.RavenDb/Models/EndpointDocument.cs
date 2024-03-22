namespace Particular.ThroughputCollector.Persistence.RavenDb.Models;

using Particular.ThroughputCollector.Contracts;

class EndpointDocument(EndpointIdentifier id)
{
    public EndpointIdentifier EndpointId { get; } = id;

    public string SanitizedName { get; set; } = string.Empty;

    public IList<string> EndpointIndicators { get; } = [];

    public string UserIndicator { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;
}
