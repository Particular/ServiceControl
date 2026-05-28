#nullable enable
namespace ServiceControl.Infrastructure.Auth.Rbac;

using System.Collections.Generic;

/// <summary>
/// The set of permission constants that are declared in <see cref="Permissions"/> but are not
/// yet enforced by any <c>[Authorize(Policy = X)]</c> attribute on a controller action.
/// <para>
/// This set serves as the "known-unenforced" allowlist for the catalogue cross-check test
/// (<c>Catalogue_completeness_tests</c>). When a permission is wired up with
/// <c>[Authorize(Policy = X)]</c>, remove it from this set; when a new unenforced constant is added
/// to <see cref="Permissions"/>, add it here until enforcement is implemented.
/// </para>
/// <para>
/// <b>On <c>tf3651-authz-s2</c>:</b> all messages &amp; recoverability permissions are enforced.
/// </para>
/// </summary>
public static class KnownUnenforcedPermissions
{
    /// <summary>
    /// Every permission constant that is declared but not yet enforced by an
    /// <c>[Authorize(Policy = X)]</c> attribute on a controller action method.
    /// </summary>
    public static readonly IReadOnlySet<string> Set = new HashSet<string>
    {
        // Messages area — fully enforced on tf3651-authz-s2; all removed from this set.
        // Recoverability groups area — fully enforced on tf3651-authz-s2; all removed.

        // Endpoints area — enforcement planned in a later phase
        Permissions.EndpointsView,
        Permissions.EndpointsManage,
        Permissions.EndpointsDelete,

        // Heartbeats area — enforcement planned in a later phase
        Permissions.HeartbeatsView,

        // Custom checks area — enforcement planned in a later phase
        Permissions.CustomChecksView,
        Permissions.CustomChecksDelete,

        // Sagas area — enforcement planned in a later phase
        Permissions.SagasView,

        // Event log area — enforcement planned in a later phase
        Permissions.EventLogView,

        // Licensing area — enforcement planned in a later phase
        Permissions.LicensingView,
        Permissions.LicensingManage,

        // Notifications area — enforcement planned in a later phase
        Permissions.NotificationsView,
        Permissions.NotificationsManage,
        Permissions.NotificationsTest,

        // Retry redirects area — enforcement planned in a later phase
        Permissions.RedirectsView,
        Permissions.RedirectsManage,

        // Queue addresses area — enforcement planned in a later phase
        Permissions.QueuesView,
        Permissions.QueuesDelete,

        // Throughput area — enforcement planned in a later phase
        Permissions.ThroughputView,
        Permissions.ThroughputManage,

        // Platform connections area — enforcement planned in a later phase
        Permissions.ConnectionsView,
        Permissions.ConnectionsManage,

        // Monitoring area — enforcement planned in a later phase
        Permissions.MonitoringView,

        // Audit area — enforcement planned in a later phase
        Permissions.AuditView,
    };
}
