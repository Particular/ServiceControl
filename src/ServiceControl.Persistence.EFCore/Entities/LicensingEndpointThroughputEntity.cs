namespace ServiceControl.Persistence.EFCore.Entities;

using Particular.LicensingComponent.Contracts;

public class LicensingEndpointThroughputEntity
{
    public required string NormalizedName { get; set; }

    public ThroughputSource ThroughputSource { get; set; }

    public DateOnly DateUtc { get; set; }

    public long MessageCount { get; set; }
}
