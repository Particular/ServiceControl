namespace ServiceControl.Infrastructure.Mcp;

using System.Collections.Generic;

public class McpCollectionResult<T>
{
    public int TotalCount { get; init; }
    public IReadOnlyCollection<T> Results { get; init; } = [];
}
