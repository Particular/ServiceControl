namespace Particular.LicensingComponent.Contracts;

/// <summary>
/// The environment data each audit remote served when last polled, one dictionary per remote that
/// responded. Never saved by versions that predate it, so a null read means unknown and the report
/// omits the keys.
/// </summary>
public record AuditEnvironmentMetadata(List<Dictionary<string, string>> Instances);
