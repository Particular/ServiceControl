#nullable enable
namespace ServiceControl.Mcp;

using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerPromptType]
public sealed class ServiceControlMcpPrompts
{
    [McpServerPrompt(Name = "servicecontrol_overview"), Description("A short orientation for the ServiceControl MCP server")]
    public static string ServiceControlOverview() => """
        Use get_errors_summary to understand the overall failure picture, then use get_failure_groups or get_failed_messages_by_endpoint to narrow the scope.
        Use get_failed_message_by_id or get_failed_message_last_attempt when you need details for a specific failed message.
        Check get_retry_history before retrying a failure group.
        Only use retry_failed_message or retry_failure_group after the underlying issue has been resolved.
        """;
}