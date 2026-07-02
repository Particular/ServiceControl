#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

public static class RolePermissions
{
    public const string Reader = "reader";
    public const string Writer = "writer";
    public const string Admin = "admin";

    static readonly string[] Read =
    [
        Permissions.ErrorMessagesView,
        Permissions.ErrorRecoverabilityGroupsView,
        Permissions.ErrorEndpointsView,
        Permissions.ErrorHeartbeatsView,
        Permissions.ErrorCustomChecksView,
        Permissions.ErrorSagasView,
        Permissions.ErrorEventLogView,
        Permissions.ErrorQueuesView,
        Permissions.ErrorConnectionsView,
        Permissions.AuditMessageView,
        Permissions.AuditConnectionView,
        Permissions.AuditEndpointView,
        Permissions.AuditSagaView,
        Permissions.MonitoringEndpointView,
        Permissions.MonitoringConnectionView,
        Permissions.MonitoringLicenseView,
    ];

    static readonly string[] ReadConfiguration =
    [
        Permissions.ErrorLicensingView,
        Permissions.ErrorNotificationsView,
        Permissions.ErrorRedirectsView,
        Permissions.ErrorThroughputView,
    ];

    static readonly string[] Operate =
    [
        Permissions.ErrorMessagesRetry,
        Permissions.ErrorMessagesArchive,
        Permissions.ErrorMessagesUnarchive,
        Permissions.ErrorMessagesEdit,
        Permissions.ErrorRecoverabilityGroupsRetry,
        Permissions.ErrorRecoverabilityGroupsArchive,
        Permissions.ErrorRecoverabilityGroupsUnarchive,
        Permissions.ErrorEndpointsManage,
        Permissions.ErrorEndpointsDelete,
        Permissions.ErrorCustomChecksDelete,
        Permissions.ErrorQueuesDelete,
        Permissions.ErrorConnectionsManage,
        Permissions.MonitoringEndpointDelete,
    ];

    static readonly string[] Configure =
    [
        Permissions.ErrorLicensingManage,
        Permissions.ErrorNotificationsManage,
        Permissions.ErrorNotificationsTest,
        Permissions.ErrorRedirectsManage,
        Permissions.ErrorThroughputManage,
    ];

    public static readonly FrozenDictionary<string, FrozenSet<string>> Roles =
        new Dictionary<string, FrozenSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Reader] = ToSet([.. Read, .. ReadConfiguration]),
            [Writer] = ToSet([.. Read, .. Operate]),
            [Admin] = ToSet([.. Read, .. ReadConfiguration, .. Operate, .. Configure]),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    static FrozenSet<string> ToSet(string[] permissions) => permissions.ToFrozenSet(StringComparer.Ordinal);
}
