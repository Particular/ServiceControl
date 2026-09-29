namespace Particular.LicensingComponent.Contracts;

public record AuditServiceMetadata(Dictionary<string, int> Versions, Dictionary<string, int> Transports)
{
    public int? ConfiguredInstances { get; init; }
    public int? LiveInstances { get; init; }
}
