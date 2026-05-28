#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Catalogue of all known permission constants in the format <c>resource:action</c>.
/// Phase 0 seeds the messages and recoverability set. Later phases extend it.
/// <para>
/// The <see cref="All"/> set is automatically derived from all <c>public const string</c>
/// fields on this class, so adding a new constant is sufficient — no separate registration needed.
/// </para>
/// </summary>
public static class Permissions
{
    /// <summary>Messages area — viewing, retrying, archiving, and editing failed messages.</summary>
    public const string MessagesView = "messages:view";
    /// <inheritdoc cref="MessagesView"/>
    public const string MessagesRetry = "messages:retry";
    /// <inheritdoc cref="MessagesView"/>
    public const string MessagesArchive = "messages:archive";
    /// <inheritdoc cref="MessagesView"/>
    public const string MessagesUnarchive = "messages:unarchive";
    /// <inheritdoc cref="MessagesView"/>
    public const string MessagesEdit = "messages:edit";

    /// <summary>Recoverability groups area — viewing, retrying, archiving, and unarchiving failure groups.</summary>
    public const string RecoverabilityGroupsView = "recoverabilitygroups:view";
    /// <inheritdoc cref="RecoverabilityGroupsView"/>
    public const string RecoverabilityGroupsRetry = "recoverabilitygroups:retry";
    /// <inheritdoc cref="RecoverabilityGroupsView"/>
    public const string RecoverabilityGroupsArchive = "recoverabilitygroups:archive";
    /// <inheritdoc cref="RecoverabilityGroupsView"/>
    public const string RecoverabilityGroupsUnarchive = "recoverabilitygroups:unarchive";

    /// <summary>Endpoints area — viewing, managing, and deleting monitored endpoints.</summary>
    public const string EndpointsView = "endpoints:view";
    /// <inheritdoc cref="EndpointsView"/>
    public const string EndpointsManage = "endpoints:manage";
    /// <inheritdoc cref="EndpointsView"/>
    public const string EndpointsDelete = "endpoints:delete";

    /// <summary>Heartbeats area — viewing heartbeat status for endpoints.</summary>
    public const string HeartbeatsView = "heartbeats:view";

    /// <summary>Custom checks area — viewing and deleting custom check results.</summary>
    public const string CustomChecksView = "customchecks:view";
    /// <inheritdoc cref="CustomChecksView"/>
    public const string CustomChecksDelete = "customchecks:delete";

    /// <summary>Sagas area — viewing saga audit data.</summary>
    public const string SagasView = "sagas:view";

    /// <summary>Event log area — viewing the event log.</summary>
    public const string EventLogView = "eventlog:view";

    /// <summary>Licensing area — viewing and managing license configuration.</summary>
    public const string LicensingView = "licensing:view";
    /// <inheritdoc cref="LicensingView"/>
    public const string LicensingManage = "licensing:manage";

    /// <summary>Notifications area — viewing, managing, and testing notification settings.</summary>
    public const string NotificationsView = "notifications:view";
    /// <inheritdoc cref="NotificationsView"/>
    public const string NotificationsManage = "notifications:manage";
    /// <inheritdoc cref="NotificationsView"/>
    public const string NotificationsTest = "notifications:test";

    /// <summary>Retry redirects area — viewing and managing message redirect rules.</summary>
    public const string RedirectsView = "redirects:view";
    /// <inheritdoc cref="RedirectsView"/>
    public const string RedirectsManage = "redirects:manage";

    /// <summary>Queue addresses area — viewing and deleting queue address entries.</summary>
    public const string QueuesView = "queues:view";
    /// <inheritdoc cref="QueuesView"/>
    public const string QueuesDelete = "queues:delete";

    /// <summary>Throughput area — viewing and managing throughput reports and settings.</summary>
    public const string ThroughputView = "throughput:view";
    /// <inheritdoc cref="ThroughputView"/>
    public const string ThroughputManage = "throughput:manage";

    /// <summary>Platform connections area — viewing and managing broker/platform connection settings.</summary>
    public const string ConnectionsView = "connections:view";
    /// <inheritdoc cref="ConnectionsView"/>
    public const string ConnectionsManage = "connections:manage";

    /// <summary>Monitoring area — read-only access to the Monitoring instance (separate process).</summary>
    public const string MonitoringView = "monitoring:view";

    /// <summary>Audit area — read-only access to the Audit instance (separate process).</summary>
    public const string AuditView = "audit:view";

    /// <summary>
    /// The complete set of known permissions, derived from all <c>public const string</c>
    /// fields declared on this class. Used by tests to assert coverage and by the completeness check.
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
