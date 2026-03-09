namespace ServiceControl.Audit.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Auditing.MessagesView;
using Infrastructure;
using ModelContextProtocol.Server;
using Persistence;

[McpServerToolType]
static class AuditTools
{
    [McpServerTool(Name = "get_audit_messages"), Description("List audited messages. Optionally exclude system messages.")]
    public static async Task<string> GetAuditMessages(
        IAuditDataStore store,
        [Description("Include system-generated messages in results.")] bool includeSystemMessages = false,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await store.GetMessages(includeSystemMessages, new PagingInfo(page, pageSize), new SortInfo("time_sent", "desc"), cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(result.Results);
    }

    [McpServerTool(Name = "get_audit_messages_by_endpoint"), Description("List audited messages received by a specific endpoint.")]
    public static async Task<string> GetAuditMessagesByEndpoint(
        IAuditDataStore store,
        [Description("The endpoint name to filter by.")] string endpointName,
        [Description("Include system-generated messages in results.")] bool includeSystemMessages = false,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await store.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpointName, new PagingInfo(page, pageSize), new SortInfo("time_sent", "desc"), cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(result.Results);
    }

    [McpServerTool(Name = "search_audit_messages"), Description("Search audited messages by a keyword across all message content.")]
    public static async Task<string> SearchAuditMessages(
        IAuditDataStore store,
        [Description("The keyword to search for.")] string keyword,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await store.QueryMessages(keyword, new PagingInfo(page, pageSize), new SortInfo("time_sent", "desc"), cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(result.Results);
    }

    [McpServerTool(Name = "search_audit_messages_by_endpoint"), Description("Search audited messages by keyword within a specific endpoint.")]
    public static async Task<string> SearchAuditMessagesByEndpoint(
        IAuditDataStore store,
        [Description("The endpoint name to search within.")] string endpointName,
        [Description("The keyword to search for.")] string keyword,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await store.QueryMessagesByReceivingEndpointAndKeyword(endpointName, keyword, new PagingInfo(page, pageSize), new SortInfo("time_sent", "desc"), cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(result.Results);
    }

    [McpServerTool(Name = "get_audit_messages_by_conversation"), Description("List audited messages belonging to a specific conversation (saga or request/response chain).")]
    public static async Task<string> GetAuditMessagesByConversation(
        IAuditDataStore store,
        [Description("The conversation ID to retrieve messages for.")] string conversationId,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await store.QueryMessagesByConversationId(conversationId, new PagingInfo(page, pageSize), new SortInfo("time_sent", "asc"), cancellationToken);
        return JsonSerializer.Serialize(result.Results);
    }

    [McpServerTool(Name = "get_audit_message_body"), Description("Get the body content of an audited message by its ID.")]
    public static async Task<string> GetAuditMessageBody(
        IAuditDataStore store,
        [Description("The unique ID of the audited message.")] string messageId,
        CancellationToken cancellationToken = default)
    {
        var result = await store.GetMessageBody(messageId, cancellationToken);
        if (!result.Found)
        {
            return "Message body not found.";
        }
        if (!result.HasContent)
        {
            return "Message body is empty.";
        }
        return result.StringContent ?? "(binary content)";
    }
}
