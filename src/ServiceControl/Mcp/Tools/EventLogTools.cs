namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Persistence;
using Persistence.Infrastructure;

[McpServerToolType]
static class EventLogTools
{
    [McpServerTool(Name = "get_event_log"), Description("Get event log items recording actions taken within ServiceControl such as retries, archives, and custom check changes.")]
    public static async Task<string> GetEventLog(
        IEventLogDataStore store,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50)
    {
        var (results, _, _) = await store.GetEventLogItems(new PagingInfo(page, pageSize));
        return JsonSerializer.Serialize(results);
    }
}
