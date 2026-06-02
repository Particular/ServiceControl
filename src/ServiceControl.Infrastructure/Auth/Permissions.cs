#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Catalogue of all known permission constants in the format <c>instance:resource:action</c>.
/// Each ServiceControl instance (error/audit/monitoring) is a separate process and namespaces its
/// permissions with an instance prefix.
/// <para>
/// The <see cref="All"/> set is automatically derived from all <c>public const string</c>
/// fields on this class, so adding a new constant is sufficient — no separate registration needed.
/// </para>
/// </summary>
public static class Permissions
{
    // ───────────────────────────── Error instance (Primary) ─────────────────────────────

    /// <summary>Messages area — viewing, retrying, archiving, and editing failed messages.</summary>
    public const string ErrorMessagesView = "error:messages:view";
    /// <inheritdoc cref="ErrorMessagesView"/>
    public const string ErrorMessagesRetry = "error:messages:retry";
    /// <inheritdoc cref="ErrorMessagesView"/>
    public const string ErrorMessagesArchive = "error:messages:archive";
    /// <inheritdoc cref="ErrorMessagesView"/>
    public const string ErrorMessagesUnarchive = "error:messages:unarchive";
    /// <inheritdoc cref="ErrorMessagesView"/>
    public const string ErrorMessagesEdit = "error:messages:edit";

    /// <summary>Recoverability groups area — viewing, retrying, archiving, and unarchiving failure groups.</summary>
    public const string ErrorRecoverabilityGroupsView = "error:recoverabilitygroups:view";
    /// <inheritdoc cref="ErrorRecoverabilityGroupsView"/>
    public const string ErrorRecoverabilityGroupsRetry = "error:recoverabilitygroups:retry";
    /// <inheritdoc cref="ErrorRecoverabilityGroupsView"/>
    public const string ErrorRecoverabilityGroupsArchive = "error:recoverabilitygroups:archive";
    /// <inheritdoc cref="ErrorRecoverabilityGroupsView"/>
    public const string ErrorRecoverabilityGroupsUnarchive = "error:recoverabilitygroups:unarchive";

    /// <summary>Endpoints area — viewing, managing, and deleting monitored endpoints.</summary>
    public const string ErrorEndpointsView = "error:endpoints:view";
    /// <inheritdoc cref="ErrorEndpointsView"/>
    public const string ErrorEndpointsManage = "error:endpoints:manage";
    /// <inheritdoc cref="ErrorEndpointsView"/>
    public const string ErrorEndpointsDelete = "error:endpoints:delete";

    /// <summary>Heartbeats area — viewing heartbeat status for endpoints.</summary>
    public const string ErrorHeartbeatsView = "error:heartbeats:view";

    /// <summary>Custom checks area — viewing and deleting custom check results.</summary>
    public const string ErrorCustomChecksView = "error:customchecks:view";
    /// <inheritdoc cref="ErrorCustomChecksView"/>
    public const string ErrorCustomChecksDelete = "error:customchecks:delete";

    /// <summary>Sagas area — viewing saga audit data.</summary>
    public const string ErrorSagasView = "error:sagas:view";

    /// <summary>Event log area — viewing the event log.</summary>
    public const string ErrorEventLogView = "error:eventlog:view";

    /// <summary>Licensing area — viewing and managing license configuration.</summary>
    public const string ErrorLicensingView = "error:licensing:view";
    /// <inheritdoc cref="ErrorLicensingView"/>
    public const string ErrorLicensingManage = "error:licensing:manage";

    /// <summary>Notifications area — viewing, managing, and testing notification settings.</summary>
    public const string ErrorNotificationsView = "error:notifications:view";
    /// <inheritdoc cref="ErrorNotificationsView"/>
    public const string ErrorNotificationsManage = "error:notifications:manage";
    /// <inheritdoc cref="ErrorNotificationsView"/>
    public const string ErrorNotificationsTest = "error:notifications:test";

    /// <summary>Retry redirects area — viewing and managing message redirect rules.</summary>
    public const string ErrorRedirectsView = "error:redirects:view";
    /// <inheritdoc cref="ErrorRedirectsView"/>
    public const string ErrorRedirectsManage = "error:redirects:manage";

    /// <summary>Queue addresses area — viewing and deleting queue address entries.</summary>
    public const string ErrorQueuesView = "error:queues:view";
    /// <inheritdoc cref="ErrorQueuesView"/>
    public const string ErrorQueuesDelete = "error:queues:delete";

    /// <summary>Throughput area — viewing and managing throughput reports and settings.</summary>
    public const string ErrorThroughputView = "error:throughput:view";
    /// <inheritdoc cref="ErrorThroughputView"/>
    public const string ErrorThroughputManage = "error:throughput:manage";

    /// <summary>Platform connections area — viewing and managing broker/platform connection settings.</summary>
    public const string ErrorConnectionsView = "error:connections:view";
    /// <inheritdoc cref="ErrorConnectionsView"/>
    public const string ErrorConnectionsManage = "error:connections:manage";

    // ───────────────────────────── Audit instance ─────────────────────────────

    /// <summary>Audit instance (separate process) — read-only audit message log.</summary>
    public const string AuditMessageView = "audit:message:view";
    /// <summary>Audit instance — viewing platform connection details.</summary>
    public const string AuditConnectionView = "audit:connection:view";
    /// <summary>Audit instance — viewing known endpoints.</summary>
    public const string AuditEndpointView = "audit:endpoint:view";
    /// <summary>Audit instance — viewing saga audit data.</summary>
    public const string AuditSagaView = "audit:saga:view";

    // ───────────────────────────── Monitoring instance ─────────────────────────────

    /// <summary>Monitoring instance (separate process) — viewing endpoint metrics.</summary>
    public const string MonitoringEndpointView = "monitoring:endpoint:view";
    /// <summary>Monitoring instance — removing a monitored endpoint instance.</summary>
    public const string MonitoringEndpointDelete = "monitoring:endpoint:delete";
    /// <summary>Monitoring instance — viewing platform connection details.</summary>
    public const string MonitoringConnectionView = "monitoring:connection:view";
    /// <summary>Monitoring instance — viewing license status.</summary>
    public const string MonitoringLicenseView = "monitoring:license:view";

    /// <summary>
    /// The complete set of known permissions, derived from all <c>public const string</c>
    /// fields declared on this class. Used by the policy provider and coverage tests.
    /// </summary>
    public static readonly IReadOnlySet<string> All = BuildAll();

    static IReadOnlySet<string> BuildAll()
    {
        var set = new HashSet<string>();
        foreach (var field in typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            {
                var value = (string?)field.GetValue(null);
                if (value != null)
                {
                    set.Add(value);
                }
            }
        }
        return set;
    }
}
