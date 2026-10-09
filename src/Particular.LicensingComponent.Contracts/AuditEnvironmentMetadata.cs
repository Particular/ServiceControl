namespace Particular.LicensingComponent.Contracts;

/// <summary>
/// The environment data each audit remote served when last polled, one dictionary per remote that
/// responded. Never saved by versions that predate it, so a null read means unknown and the report
/// omits the keys.
/// </summary>
public record AuditEnvironmentMetadata(List<Dictionary<string, string>> Instances)
{
    /// <summary>
    /// The key under which the primary stores which database an instance's data came from, so that
    /// instances sharing a database are counted once when sizes and counts are summed. Underscore
    /// prefixed keys are collection bookkeeping and never reach the report.
    /// </summary>
    public const string DatabaseKey = "_DatabaseKey";
}
