#nullable enable
namespace ServiceControl.Mcp;

using System.Collections.Generic;

public sealed class McpCollectionResult<T>
{
    public int TotalCount { get; init; }
    public IReadOnlyCollection<T> Results { get; init; } = [];
}