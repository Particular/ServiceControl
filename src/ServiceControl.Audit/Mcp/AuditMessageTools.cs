#nullable enable

namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Persistence;

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
    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for browsing successfully processed audit messages when the user wants an overview rather than a text search. " +
        "Good for questions like: 'show recent audit messages', 'what messages were processed today?', 'list messages from endpoint X', or 'show slow messages'. " +
        "Returns message metadata such as message type, endpoints, sent time, processed time, and timing metrics. " +
        "For broad requests, use the default paging and sorting. " +
        "Prefer this tool over SearchAuditMessages when the user does not provide a specific keyword or phrase. " +
        "If the user is looking for a specific term, id, or text fragment, use SearchAuditMessages instead."
    )]
    public async Task<string> GetAuditMessages(
        [Description("Set to true to include NServiceBus infrastructure messages. Leave this as false for the usual business-message view.")] bool includeSystemMessages = false,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        [Description("Filter audit messages to those sent after this ISO 8601 date/time. Use with timeSentTo for a bounded time window.")] string? timeSentFrom = null,
        [Description("Filter audit messages to those sent before this ISO 8601 date/time. Omit to leave the upper bound open.")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessages invoked (page={Page}, includeSystemMessages={IncludeSystem})", page, includeSystemMessages);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = await store.GetMessages(includeSystemMessages, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        logger.LogInformation("MCP GetAuditMessages returned {Count} results", results.QueryStats.TotalCount);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for finding audit messages by a keyword or phrase. " +
        "Good for questions like: 'find messages containing order 12345', 'search for CustomerCreated messages', or 'look for messages mentioning this ID'. " +
        "Searches across message body content, headers, and metadata using full-text search. " +
        "Prefer this tool over GetAuditMessages when the user provides a specific term, identifier, or phrase to search for. " +
        "If the user just wants to browse recent messages without a search term, use GetAuditMessages instead."
    )]
    public async Task<string> SearchAuditMessages(
        [Description("The free-text search query to match against audit message body content, headers, and metadata.")] string query,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        [Description("Filter audit search results to messages sent after this ISO 8601 date/time.")] string? timeSentFrom = null,
        [Description("Filter audit search results to messages sent before this ISO 8601 date/time.")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP SearchAuditMessages invoked (query={Query}, page={Page})", query, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = await store.QueryMessages(query, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        logger.LogInformation("MCP SearchAuditMessages returned {Count} results", results.QueryStats.TotalCount);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for seeing what messages a specific NServiceBus endpoint has processed. " +
        "Good for questions like: 'what messages did Sales process?', 'show messages handled by Shipping', or 'find OrderPlaced messages in the Billing endpoint'. " +
        "Returns the same metadata as GetAuditMessages but scoped to one endpoint. " +
        "Prefer this tool over GetAuditMessages when the user mentions a specific endpoint name. " +
        "Optionally pass a keyword to search within that endpoint's messages."
    )]
    public async Task<string> GetAuditMessagesByEndpoint(
        [Description("The NServiceBus endpoint name to investigate, for example 'Sales' or 'Shipping.MessageHandler'.")] string endpointName,
        [Description("Optional keyword to narrow results within this endpoint. Omit it to browse the endpoint without full-text filtering.")] string? keyword = null,
        [Description("Set to true to include NServiceBus infrastructure messages for this endpoint. Leave false for the usual business-message view.")] bool includeSystemMessages = false,
        [Description("Page number, 1-based")] int page = 1,
        [Description("Results per page")] int perPage = 50,
        [Description("Sort by: time_sent, processed_at, message_type, critical_time, delivery_time, or processing_time")] string sort = "time_sent",
        [Description("Sort direction: asc or desc")] string direction = "desc",
        [Description("Filter endpoint audit messages to those sent after this ISO 8601 date/time.")] string? timeSentFrom = null,
        [Description("Filter endpoint audit messages to those sent before this ISO 8601 date/time.")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessagesByEndpoint invoked (endpoint={EndpointName}, keyword={Keyword}, page={Page})", endpointName, keyword, page);

        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = keyword != null
            ? await store.QueryMessagesByReceivingEndpointAndKeyword(endpointName, keyword, pagingInfo, sortInfo, timeSentRange, cancellationToken)
            : await store.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpointName, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        logger.LogInformation("MCP GetAuditMessagesByEndpoint returned {Count} results for endpoint '{EndpointName}'", results.QueryStats.TotalCount, endpointName);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for tracing the full chain of audit messages triggered by an initial message. " +
        "Good for questions like: 'what happened after this message was sent?', 'show me the full message flow', or 'trace this conversation'. " +
        "A conversation groups all related messages together — the original command and every event, reply, or saga message it caused. " +
        "You need a conversation ID, which you can get from any audit message query result. " +
        "Essential for understanding message flow and debugging cascading issues."
    )]
    public async Task<string> GetAuditMessagesByConversation(
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

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "This is a read-only tool for inspecting the actual payload of a processed audit message. " +
        "Good for questions like: 'show me the message body', 'what data was in this message?', or 'let me see the content of message X'. " +
        "Returns the serialized message body content, typically JSON. " +
        "You need an audit message ID, which you can get from any audit message query result. " +
        "Use this when the user wants to see what data was actually sent, not just message metadata."
    )]
    public async Task<string> GetAuditMessageBody(
        [Description("The audit message ID from a previous audit message query result.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MCP GetAuditMessageBody invoked (messageId={MessageId})", messageId);

        var result = await store.GetMessageBody(messageId, cancellationToken);

        if (!result.Found)
        {
            logger.LogWarning("MCP GetAuditMessageBody: message '{MessageId}' not found", messageId);
            return JsonSerializer.Serialize(new { Error = $"Message '{messageId}' not found." }, McpJsonOptions.Default);
        }

        if (!result.HasContent)
        {
            logger.LogWarning("MCP GetAuditMessageBody: message '{MessageId}' has no body content", messageId);
            return JsonSerializer.Serialize(new { Error = $"Message '{messageId}' has no body content." }, McpJsonOptions.Default);
        }

        if (result.StringContent != null)
        {
            return JsonSerializer.Serialize(new
            {
                result.ContentType,
                result.ContentLength,
                Body = result.StringContent
            }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(new
        {
            result.ContentType,
            result.ContentLength,
            Body = "(stream content - not available as text)"
        }, McpJsonOptions.Default);
    }
}
