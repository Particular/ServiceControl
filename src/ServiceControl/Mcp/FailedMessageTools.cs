#nullable enable

namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using MessageFailures.Api;
using ModelContextProtocol.Server;
using Persistence;
using Persistence.Infrastructure;

[McpServerToolType]
public class FailedMessageTools(IErrorMessageDataStore store)
{
    [McpServerTool, Description("Get a list of failed messages. Supports filtering by status (unresolved, resolved, archived, retryissued), modified date, and queue address. Returns paged results.")]
    public async Task<string> GetFailedMessages(
        [Description("Filter by status: unresolved, resolved, archived, retryissued")] string? status = null,
        [Description("Filter by modified date (ISO 8601 format)")] string? modified = null,
        [Description("Filter by queue address")] string? queueAddress = null,
        [Description("Page number (1-based). Default is 1")] int page = 1,
        [Description("Number of results per page. Default is 50")] int perPage = 50,
        [Description("Sort field: time_sent, message_type, time_of_failure. Default is time_of_failure")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc. Default is desc")] string direction = "desc")
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.ErrorGet(status, modified, queueAddress, pagingInfo, sortInfo);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get details of a specific failed message by its unique ID.")]
    public async Task<string> GetFailedMessageById(
        [Description("The unique ID of the failed message")] string failedMessageId)
    {
        var result = await store.ErrorBy(failedMessageId);

        if (result == null)
        {
            return JsonSerializer.Serialize(new { Error = $"Failed message '{failedMessageId}' not found." }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get the last processing attempt for a specific failed message.")]
    public async Task<string> GetFailedMessageLastAttempt(
        [Description("The unique ID of the failed message")] string failedMessageId)
    {
        var result = await store.ErrorLastBy(failedMessageId);

        if (result == null)
        {
            return JsonSerializer.Serialize(new { Error = $"Failed message '{failedMessageId}' not found." }, McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get a summary of error counts grouped by status (unresolved, archived, resolved, retryissued).")]
    public async Task<string> GetErrorsSummary()
    {
        var result = await store.ErrorsSummary();
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get failed messages for a specific endpoint.")]
    public async Task<string> GetFailedMessagesByEndpoint(
        [Description("The name of the endpoint")] string endpointName,
        [Description("Filter by status: unresolved, resolved, archived, retryissued")] string? status = null,
        [Description("Filter by modified date (ISO 8601 format)")] string? modified = null,
        [Description("Page number (1-based). Default is 1")] int page = 1,
        [Description("Number of results per page. Default is 50")] int perPage = 50,
        [Description("Sort field: time_sent, message_type, time_of_failure. Default is time_of_failure")] string sort = "time_of_failure",
        [Description("Sort direction: asc or desc. Default is desc")] string direction = "desc")
    {
        var pagingInfo = new PagingInfo(page, perPage);
        var sortInfo = new SortInfo(sort, direction);

        var results = await store.ErrorsByEndpointName(status, endpointName, modified, pagingInfo, sortInfo);

        return JsonSerializer.Serialize(new
        {
            results.QueryStats.TotalCount,
            results.Results
        }, McpJsonOptions.Default);
    }
}
