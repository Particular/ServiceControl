#nullable enable
namespace ServiceControl.Mcp.Authorization;

using ServiceControl.Infrastructure.Auth;

/// <summary>
/// MCP tool-to-permission mapping that intentionally reuses the existing ServiceControl permission
/// constants rather than inventing a separate auth model.
/// </summary>
public static class McpPermissions
{
    public const string ListFailures = Permissions.ErrorMessagesView;
    public const string GetFailure = Permissions.ErrorMessagesView;
    public const string RetryFailure = Permissions.ErrorMessagesRetry;

    public const string ListFailureGroups = Permissions.ErrorRecoverabilityGroupsView;
    public const string GetFailureGroup = Permissions.ErrorRecoverabilityGroupsView;
    public const string RetryFailureGroup = Permissions.ErrorRecoverabilityGroupsRetry;
}