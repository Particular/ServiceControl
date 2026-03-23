#nullable enable

namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Persistence;
using Recoverability;

[McpServerToolType, Description(
    "Tools for inspecting failure groups and retry history.\n\n" +
    "Agent guidance:\n" +
    "1. GetFailureGroups is usually the best starting point for diagnosing production issues — call it before drilling into individual messages.\n" +
    "2. Call GetFailureGroups with no parameters to use the default grouping by exception type and stack trace.\n" +
    "3. Use GetRetryHistory to check whether someone has already retried a group before retrying it again."
)]
public class FailureGroupTools(GroupFetcher fetcher, IRetryHistoryDataStore retryStore)
{
    [McpServerTool, Description(
        "Use this tool to understand why messages are failing by seeing failures grouped by root cause. " +
        "Good for questions like: 'why are messages failing?', 'what errors are happening?', 'group failures by exception', or 'what are the top failure causes?'. " +
        "Each group represents a distinct exception type and stack trace, showing how many messages are affected and when failures started and last occurred. " +
        "This is usually the best starting point for diagnosing production issues — call it before drilling into individual messages. " +
        "Call with no parameters to use the default grouping by exception type and stack trace."
    )]
    public async Task<string> GetFailureGroups(
        [Description("How to group failures. The default 'Exception Type and Stack Trace' is almost always what you want. Use 'Message Type' to group by the NServiceBus message type instead.")] string classifier = "Exception Type and Stack Trace",
        [Description("Only include groups matching this filter text")] string? classifierFilter = null)
    {
        var results = await fetcher.GetGroups(classifier, classifierFilter);
        return JsonSerializer.Serialize(results, McpJsonOptions.Default);
    }

    [McpServerTool, Description(
        "Use this tool to check the history of retry operations. " +
        "Good for questions like: 'has someone already retried these?', 'what happened the last time we retried this group?', 'show retry history', or 'were any retries attempted today?'. " +
        "Returns which groups were retried, when, and whether the retries succeeded or failed. " +
        "Use this before retrying a group to avoid duplicate retry attempts."
    )]
    public async Task<string> GetRetryHistory()
    {
        var retryHistory = await retryStore.GetRetryHistory();
        return JsonSerializer.Serialize(retryHistory, McpJsonOptions.Default);
    }
}
