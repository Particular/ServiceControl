namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Auth;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    [Authorize]
    public class MeController(Settings settings) : ControllerBase
    {
        [HttpGet]
        [Route("my/permissions/all")]
        public ActionResult<PermissionsDescriptor> GetMyPermissions()
        {
            var descriptor = new PermissionsDescriptor(
                User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "unknown",
                GrantedPermissions().OrderBy(p => p, StringComparer.Ordinal).ToList());

            return Ok(descriptor);
        }

        [HttpGet]
        [Route("my/permissions")]
        public ActionResult<PermissionsSummary> GetSummaryPermissions()
        {
            var granted = GrantedPermissions();

            var summary = new PermissionsSummary(
                FailedMessagesRead: HasView(granted, IsFailedMessagesResource),
                FailedMessagesWrite: HasWrite(granted, IsFailedMessagesResource),
                AuditingRead: HasView(granted, IsAuditResource),
                MonitoringRead: HasView(granted, IsMonitoringResource),
                MonitoringWrite: HasWrite(granted, IsMonitoringResource),
                AdminRead: HasView(granted, IsAdminResource),
                AdminWrite: HasWrite(granted, IsAdminResource));

            return Ok(summary);
        }

        // The set of permissions the current user holds, taking the RBAC-disabled "allow everything"
        // mode (mirrors PermissionPolicyProvider's allow-all policy) into account.
        IReadOnlySet<string> GrantedPermissions()
        {
            var oidc = settings.OpenIdConnectSettings;

            return oidc.RoleBasedAuthorizationEnabled
                ? RolePermissions.GetPermissions(User.FindAll(oidc.RolesClaim).Select(c => c.Value))
                : Permissions.All;
        }

        static bool HasView(IEnumerable<string> permissions, Func<string, bool> inGroup) =>
            permissions.Any(p => inGroup(p) && p.EndsWith(":view", StringComparison.Ordinal));

        static bool HasWrite(IEnumerable<string> permissions, Func<string, bool> inGroup) =>
            permissions.Any(p => inGroup(p) && !p.EndsWith(":view", StringComparison.Ordinal));

        // Resources within the "error" instance that belong to the settings/admin area rather than the
        // failure-triage area. Everything else under "error:" (messages, recoverability groups, endpoints,
        // heartbeats, custom checks, sagas, event log, queues) is one bundle: a user either has access to
        // all of failure-triage, or none of it.
        static readonly string[] AdminResources = ["licensing", "notifications", "redirects", "throughput"];

        static bool IsAdminResource(string permission) =>
            AdminResources.Any(resource => permission.StartsWith($"error:{resource}:", StringComparison.Ordinal));

        static bool IsFailedMessagesResource(string permission) =>
            permission.StartsWith("error:", StringComparison.Ordinal) && !IsAdminResource(permission);

        static bool IsAuditResource(string permission) =>
            permission.StartsWith("audit:", StringComparison.Ordinal);

        static bool IsMonitoringResource(string permission) =>
            permission.StartsWith("monitoring:", StringComparison.Ordinal);

        public sealed record PermissionsDescriptor(string User, IReadOnlyList<string> Permissions);

        public sealed record PermissionsSummary(
            bool FailedMessagesRead,
            bool FailedMessagesWrite,
            bool AuditingRead,
            bool MonitoringRead,
            bool MonitoringWrite,
            bool AdminRead,
            bool AdminWrite);
    }
}