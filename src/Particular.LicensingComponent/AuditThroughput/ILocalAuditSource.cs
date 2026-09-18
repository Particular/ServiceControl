namespace Particular.LicensingComponent.AuditThroughput;

using Particular.LicensingComponent.Contracts;

/// <summary>
/// Audit throughput collection is driven entirely by audit remotes. A primary that holds audit data
/// itself has no remote to describe it, so without this its own audit queues are counted as customer
/// endpoints and the audit service metadata in the licensing report is blank.
/// </summary>
public interface ILocalAuditSource
{
    bool Enabled { get; }

    RemoteInstanceInformation Describe();
}
