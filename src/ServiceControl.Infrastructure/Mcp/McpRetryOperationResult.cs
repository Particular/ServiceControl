#nullable enable

namespace ServiceControl.Infrastructure.Mcp;

public class McpRetryOperationResult : McpOperationResult
{
    public static McpRetryOperationResult Accepted(string message) => Accepted<McpRetryOperationResult>(message);

    public static McpRetryOperationResult InProgress(string message) => InProgress<McpRetryOperationResult>(message);

    public static McpRetryOperationResult ValidationError(string error) => ValidationError<McpRetryOperationResult>(error);
}
