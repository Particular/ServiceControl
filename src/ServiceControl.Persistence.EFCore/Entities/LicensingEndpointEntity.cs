namespace ServiceControl.Persistence.EFCore.Entities;

using Particular.LicensingComponent.Contracts;

public class LicensingEndpointEntity
{
    public required string NormalizedName { get; set; }

    public ThroughputSource ThroughputSource { get; set; }

    public required string Name { get; set; }

    public required string SanitizedName { get; set; }

    public required string NormalizedSanitizedName { get; set; }

    public string UserIndicator { get; set; } = string.Empty;

    public string? Scope { get; set; }

    public List<string> EndpointIndicators { get; set; } = [];
}
