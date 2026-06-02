#nullable enable

namespace ServiceControl.Infrastructure.Mcp;

public class McpAuditMessageBodyResult
{
    public string? ContentType { get; init; }
    public int ContentLength { get; init; }
    public string? Body { get; init; }
    public string? Error { get; init; }
}
