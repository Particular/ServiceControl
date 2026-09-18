#nullable enable
namespace ServiceControl.Mcp;

public static class McpServerConfiguration
{
    public const string Route = "/mcp";

    public const string CorsPolicyName = "servicecontrol-mcp";

    public const string ServerInstructions = "ServiceControl documentation is available through the failure and recoverability tools. Start with get_errors_summary or get_failure_groups, then drill into a specific failed message, failure group, or retry history. Retry tools are write operations and should only be used after the underlying issue has been resolved.";
}