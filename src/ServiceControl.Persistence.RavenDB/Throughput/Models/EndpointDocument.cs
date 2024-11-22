#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput.Models;

using System.Collections.Generic;
using Particular.LicensingComponent.Contracts;

class EndpointDocument(EndpointIdentifier id)
{
    public string Id { get; set; } = id.GenerateDocumentId();

    public EndpointIdentifier EndpointId { get; set; } = id;

    public string SanitizedName { get; set; } = string.Empty;

    public IList<string> EndpointIndicators { get; } = [];

    public string UserIndicator { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;
}