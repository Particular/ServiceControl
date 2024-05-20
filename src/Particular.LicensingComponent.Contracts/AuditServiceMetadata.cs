namespace Particular.LicensingComponent.Contracts;

public record AuditServiceMetadata(Dictionary<string, int> Versions, Dictionary<string, int> Transports)
{
}