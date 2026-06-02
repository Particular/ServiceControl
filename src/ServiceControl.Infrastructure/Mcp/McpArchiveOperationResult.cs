#nullable enable

namespace ServiceControl.Infrastructure.Mcp;

public class McpArchiveOperationResult : McpOperationResult
{
    public static McpArchiveOperationResult Accepted(string message) => Accepted<McpArchiveOperationResult>(message);

    public static McpArchiveOperationResult InProgress(string message) => InProgress<McpArchiveOperationResult>(message);

    public static McpArchiveOperationResult ValidationError(string error) => ValidationError<McpArchiveOperationResult>(error);
}
