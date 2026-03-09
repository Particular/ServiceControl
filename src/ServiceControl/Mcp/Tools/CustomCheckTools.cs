namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Persistence;
using Persistence.Infrastructure;

[McpServerToolType]
static class CustomCheckTools
{
    [McpServerTool(Name = "get_custom_checks"), Description("List custom check results reported by endpoints. Optionally filter by status.")]
    public static async Task<string> GetCustomChecks(
        ICustomChecksDataStore store,
        [Description("Status filter: Pass or Fail.")] string status = null,
        [Description("Page number (1-based).")] int page = 1,
        [Description("Results per page (default 50).")] int pageSize = 50)
    {
        var stats = await store.GetStats(new PagingInfo(page, pageSize), status);
        return JsonSerializer.Serialize(stats.Results);
    }
}
