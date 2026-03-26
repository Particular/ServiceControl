#nullable enable

namespace ServiceControl.Infrastructure.Mcp;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum McpOperationStatus
{
    Accepted,
    InProgress,
    ValidationError
}

public class McpOperationResult
{
    public McpOperationStatus Status { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }

    protected static T Accepted<T>(string message) where T : McpOperationResult, new() => new()
    {
        Status = McpOperationStatus.Accepted,
        Message = message
    };

    protected static T InProgress<T>(string message) where T : McpOperationResult, new() => new()
    {
        Status = McpOperationStatus.InProgress,
        Message = message
    };

    protected static T ValidationError<T>(string error) where T : McpOperationResult, new() => new()
    {
        Status = McpOperationStatus.ValidationError,
        Error = error
    };
}
