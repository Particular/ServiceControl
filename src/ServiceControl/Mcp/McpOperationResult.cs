#nullable enable
namespace ServiceControl.Mcp;

public sealed class McpOperationResult
{
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public static McpOperationResult Accepted(string message) => new() { Status = "accepted", Message = message };

    public static McpOperationResult InProgress(string message) => new() { Status = "in_progress", Message = message };

    public static McpOperationResult ValidationError(string message) => new() { Status = "validation_error", Message = message };
}