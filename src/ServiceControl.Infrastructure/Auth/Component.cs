#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>
/// The functional area a permission applies to, flat across all instances. The wire value is the name
/// lowercased (e.g. <see cref="RecoverabilityGroups"/> → <c>recoverabilitygroups</c>).
/// <para>
/// The set is flat (not nested per instance), so not every <see cref="Component"/> is valid for every
/// <see cref="InstanceId"/>. The error instance uses the plural forms (<see cref="Messages"/>,
/// <see cref="Endpoints"/>, …) while the audit and monitoring instances use the singular forms
/// (<see cref="Message"/>, <see cref="Endpoint"/>, …). Validity of a full
/// <c>instance:component:access</c> triple is enforced by <see cref="PermissionId.TryParse"/> against
/// the known catalogue.
/// </para>
/// </summary>
public enum Component
{
    // Error instance (plural).
    Messages,
    RecoverabilityGroups,
    Endpoints,
    Heartbeats,
    CustomChecks,
    Sagas,
    EventLog,
    Licensing,
    Notifications,
    Redirects,
    Queues,
    Throughput,
    Connections,

    // Audit / Monitoring instances (singular).
    Message,
    Connection,
    Endpoint,
    Saga,
    License
}
