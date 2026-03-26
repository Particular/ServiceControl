#nullable enable

namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Auditing.MessagesView;
using Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Persistence;
using ServiceControl.Infrastructure.Mcp;

[McpServerToolType, Description(
    "Read-only tools for exploring audit messages.\n\n" +
    "Agent guidance:\n" +
    "1. For broad requests like 'show recent messages', start with GetAuditMessages using defaults.\n" +
    "2. For requests containing a concrete text term, identifier, or phrase, use SearchAuditMessages.\n" +
    "3. Keep page=1 unless the user asks for more results.\n" +
    "4. Keep perPage modest, such as 20 to 50, unless the user asks for a larger batch.\n" +
    "5. Use time filters when the user mentions a date or time window like 'today' or 'last hour'.\n" +
    "6. Only change sorting when the user explicitly asks for it."
)]
public class AuditMessageTools(IAuditDataStore store, ILogger<AuditMessageTools> logger)
{
    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve audit messages with paging and sorting. " +
        "Use this to browse recent message activity or explore message flow over time. " +
        "Prefer SearchAuditMessages when looking for specific keywords or content. " +
        "Read-only."
    )]
    public async Task<McpCollectionResult<MessagesView>> GetAuditMessages(
        [Description("Set to true to include NServiceBus infrastructure messages. Leave this as false for the usual business-message view.")] bool includeSystemMessages = false,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        [Description("Restricts audit-message results to messages sent after this ISO 8601 date/time. Omitting this may return a large result set.")] string? timeSentFrom = null,
        [Description("Restricts audit-message results to messages sent before this ISO 8601 date/time. Omitting this may return a large result set.")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessages invoked (page={Page}, includeSystemMessages={IncludeSystem})", page, includeSystemMessages);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = await store.GetMessages(includeSystemMessages, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        logger.LogInformation("MCP GetAuditMessages returned {Count} results", results.QueryStats.TotalCount);

        return new McpCollectionResult<MessagesView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results.ToArray()
        };
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Search audit messages by keyword across message content and metadata. " +
        "Use this when trying to locate messages related to a specific business identifier or text. " +
        "Prefer GetAuditMessages for general browsing or timeline exploration. " +
        "Read-only."
    )]
    public async Task<McpCollectionResult<MessagesView>> SearchAuditMessages(
        [Description("The free-text search query to match against audit message body content, headers, and metadata.")] string query,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        [Description("Restricts audit search results to messages sent after this ISO 8601 date/time. Omitting this may return a large result set.")] string? timeSentFrom = null,
        [Description("Restricts audit search results to messages sent before this ISO 8601 date/time. Omitting this may return a large result set.")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP SearchAuditMessages invoked (query={Query}, page={Page})", query, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = await store.QueryMessages(query, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        logger.LogInformation("MCP SearchAuditMessages returned {Count} results", results.QueryStats.TotalCount);

        return new McpCollectionResult<MessagesView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results.ToArray()
        };
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve audit messages processed by a specific endpoint. " +
        "Use this to understand activity and behavior of a single endpoint. " +
        "Prefer GetAuditMessagesByConversation when tracing a specific message flow. " +
        "Read-only."
    )]
    public async Task<McpCollectionResult<MessagesView>> GetAuditMessagesByEndpoint(
        [Description("The endpoint name that processed the audit messages. Use values obtained from GetKnownEndpoints.")] string endpointName,
        [Description("Optional keyword to narrow results within this endpoint. Omit it to browse the endpoint without full-text filtering.")] string? keyword = null,
        [Description("Set to true to include NServiceBus infrastructure messages for this endpoint. Leave false for the usual business-message view.")] bool includeSystemMessages = false,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        [Description("Restricts endpoint audit-message results to messages sent after this ISO 8601 date/time. Omitting this may return a large result set.")] string? timeSentFrom = null,
        [Description("Restricts endpoint audit-message results to messages sent before this ISO 8601 date/time. Omitting this may return a large result set.")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessagesByEndpoint invoked (endpoint={EndpointName}, keyword={Keyword}, page={Page})", endpointName, keyword, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = keyword != null
            ? await store.QueryMessagesByReceivingEndpointAndKeyword(includeSystemMessages, endpointName, keyword, pagingInfo, sortInfo, timeSentRange, cancellationToken)
            : await store.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpointName, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        logger.LogInformation("MCP GetAuditMessagesByEndpoint returned {Count} results for endpoint '{EndpointName}'", results.QueryStats.TotalCount, endpointName);

        return new McpCollectionResult<MessagesView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results.ToArray()
        };
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve all audit messages belonging to a conversation. " +
        "Use this to trace the full flow of a message or business process across multiple endpoints. " +
        "Prefer this tool when you already have a conversation ID. " +
        "Read-only."
    )]
    public async Task<McpCollectionResult<MessagesView>> GetAuditMessagesByConversation(
        [Description("The conversation ID from a previous audit message query result.")] string conversationId,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessagesByConversation invoked (conversationId={ConversationId}, page={Page})", conversationId, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.QueryMessagesByConversationId(conversationId, pagingInfo, sortInfo, cancellationToken);

        logger.LogInformation("MCP GetAuditMessagesByConversation returned {Count} results", results.QueryStats.TotalCount);

        return new McpCollectionResult<MessagesView>
        {
            TotalCount = (int)results.QueryStats.TotalCount,
            Results = results.Results.ToArray()
        };
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true), Description(
        "Retrieve the body content of a specific audit message. " +
        "Use this when you need to inspect message payload or data for debugging. " +
        "Typically used after locating a message via search or browsing tools. " +
        "Read-only."
    )]
    public async Task<McpAuditMessageBodyResult> GetAuditMessageBody(
        [Description("The audit message ID from a previous audit message query result.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessageBody invoked (messageId={MessageId})", messageId);

        var result = await store.GetMessageBody(messageId, cancellationToken);

        if (!result.Found)
        {
            logger.LogWarning("MCP GetAuditMessageBody: message '{MessageId}' not found", messageId);
            return new McpAuditMessageBodyResult
            {
                Error = $"Message '{messageId}' not found."
            };
        }

        if (!result.HasContent)
        {
            logger.LogWarning("MCP GetAuditMessageBody: message '{MessageId}' has no body content", messageId);
            return new McpAuditMessageBodyResult
            {
                Error = $"Message '{messageId}' has no body content."
            };
        }

        if (result.StringContent != null)
        {
            return new McpAuditMessageBodyResult
            {
                ContentType = result.ContentType,
                ContentLength = result.ContentLength,
                Body = result.StringContent
            };
        }

        return new McpAuditMessageBodyResult
        {
            ContentType = result.ContentType,
            ContentLength = result.ContentLength,
            Body = "(stream content - not available as text)"
        };
    }
}
