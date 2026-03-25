#nullable enable

namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Persistence;
using Recoverability;

[McpServerToolType, Description(
    "Read-only tools for inspecting failure groups and retry history.\n\n" +
    "Agent guidance:\n" +
    "1. GetFailureGroups is usually the best starting point for diagnosing production issues — call it before drilling into individual messages.\n" +
    "2. Call GetFailureGroups with no parameters to use the default grouping by exception type and stack trace.\n" +
    "3. Use GetRetryHistory to check whether someone has already retried a group before retrying it again."
)]
public class FailureGroupTools(GroupFetcher fetcher, IRetryHistoryDataStore retryStore, ILogger<FailureGroupTools> logger)
{
    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Retrieve failure groups, where failed messages are grouped by exception type and stack trace. " +
        "Use this as the first step when analyzing large numbers of failures to identify dominant root causes. " +
        "Prefer GetFailedMessages when you need individual message details. " +
        "Read-only."
    )]
    public async Task<string> GetFailureGroups(
        [Description("How to group failures. The default 'Exception Type and Stack Trace' is almost always what you want. Use 'Message Type' to group by the NServiceBus message type instead.")] string classifier = "Exception Type and Stack Trace",
        [Description("Filter failure groups by classifier text. Omit this filter to include all groups for the selected classifier.")] string? classifierFilter = null)
    {
        logger.LogInformation("MCP GetFailureGroups invoked (classifier={Classifier})", classifier);

        var results = await fetcher.GetGroups(classifier, classifierFilter);

        logger.LogInformation("MCP GetFailureGroups returned {Count} groups", results.Length);

        return JsonSerializer.Serialize(results, McpJsonOptions.Default);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false), Description(
        "Use this tool to check the history of retry operations. " +
        "Good for questions like: 'has someone already retried these?', 'what happened the last time we retried this group?', 'show retry history', or 'were any retries attempted today?'. " +
        "Returns which groups were retried, when, and whether the retries succeeded or failed. " +
        "Use this before retrying a group to avoid duplicate retry attempts. " +
        "Read-only."
    )]
    public async Task<string> GetRetryHistory()
    {
        logger.LogInformation("MCP GetRetryHistory invoked");

        var retryHistory = await retryStore.GetRetryHistory();
        return JsonSerializer.Serialize(retryHistory, McpJsonOptions.Default);
    }
}
