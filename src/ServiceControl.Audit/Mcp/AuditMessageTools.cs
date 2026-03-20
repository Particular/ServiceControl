#nullable enable

namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using ModelContextProtocol.Server;
using Persistence;

[McpServerToolType]
public class AuditMessageTools(IAuditDataStore store)
{
    [McpServerTool, Description("Get a list of successfully processed audit messages. Supports paging and sorting. Returns message metadata including endpoints, timing information, and message type.")]
    public async Task<string> GetAuditMessages(
        [Description("Whether to include system messages in results. Default is false")] bool includeSystemMessages = false,
        [Description("Page number (1-based). Default is 1")] int page = 1,
        [Description("Number of results per page. Default is 50")] int perPage = 50,
        [Description("Sort field: time_sent, processed_at, message_type, critical_time, delivery_time, processing_time. Default is time_sent")] string sort = "time_sent",
        [Description("Sort direction: asc or desc. Default is desc")] string direction = "desc",
        [Description("Filter by time sent start (ISO 8601 format)")] string? timeSentFrom = null,
        [Description("Filter by time sent end (ISO 8601 format)")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = await store.GetMessages(includeSystemMessages, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Search audit messages by a keyword or phrase. Searches across message content and metadata.")]
    public async Task<string> SearchAuditMessages(
        [Description("The search query string")] string query,
        [Description("Page number (1-based). Default is 1")] int page = 1,
        [Description("Number of results per page. Default is 50")] int perPage = 50,
        [Description("Sort field: time_sent, processed_at, message_type, critical_time, delivery_time, processing_time. Default is time_sent")] string sort = "time_sent",
        [Description("Sort direction: asc or desc. Default is desc")] string direction = "desc",
        [Description("Filter by time sent start (ISO 8601 format)")] string? timeSentFrom = null,
        [Description("Filter by time sent end (ISO 8601 format)")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = await store.QueryMessages(query, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get audit messages received by a specific endpoint. Can optionally filter by keyword.")]
    public async Task<string> GetAuditMessagesByEndpoint(
        [Description("The name of the receiving endpoint")] string endpointName,
        [Description("Optional keyword to filter messages")] string? keyword = null,
        [Description("Whether to include system messages in results. Default is false")] bool includeSystemMessages = false,
        [Description("Page number (1-based). Default is 1")] int page = 1,
        [Description("Number of results per page. Default is 50")] int perPage = 50,
        [Description("Sort field: time_sent, processed_at, message_type, critical_time, delivery_time, processing_time. Default is time_sent")] string sort = "time_sent",
        [Description("Sort direction: asc or desc. Default is desc")] string direction = "desc",
        [Description("Filter by time sent start (ISO 8601 format)")] string? timeSentFrom = null,
        [Description("Filter by time sent end (ISO 8601 format)")] string? timeSentTo = null,
        CancellationToken cancellationToken = default)
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);
        var timeSentRange = new DateTimeRange(timeSentFrom, timeSentTo);

        var results = keyword != null
            ? await store.QueryMessagesByReceivingEndpointAndKeyword(endpointName, keyword, pagingInfo, sortInfo, timeSentRange, cancellationToken)
            : await store.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpointName, pagingInfo, sortInfo, timeSentRange, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get all audit messages that belong to a specific conversation. A conversation groups related messages that were triggered by the same initial message.")]
    public async Task<string> GetAuditMessagesByConversation(
        [Description("The conversation ID to filter by")] string conversationId,
        [Description("Page number (1-based). Default is 1")] int page = 1,
        [Description("Number of results per page. Default is 50")] int perPage = 50,
        [Description("Sort field: time_sent, processed_at, message_type, critical_time, delivery_time, processing_time. Default is time_sent")] string sort = "time_sent",
        [Description("Sort direction: asc or desc. Default is desc")] string direction = "desc",
        CancellationToken cancellationToken = default)
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.QueryMessagesByConversationId(conversationId, pagingInfo, sortInfo, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get the body content of a specific audit message by its message ID.")]
    public async Task<string> GetAuditMessageBody(
        [Description("The message ID")] string messageId,
        CancellationToken cancellationToken = default)
    {
        var result = await store.GetMessageBody(messageId, cancellationToken);

        if (!result.Found)
        {
            return JsonSerializer.Serialize(new { Error = $"Message '{messageId}' not found." }, McpJsonOptions.Default);
        }

        if (!result.HasContent)
        {
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
