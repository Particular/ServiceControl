#nullable enable

namespace ServiceControl.Mcp;

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Persistence;
using Recoverability;

[McpServerToolType]
public class FailureGroupTools(GroupFetcher fetcher, IRetryHistoryDataStore retryStore)
{
    [McpServerTool, Description("Get failure groups, which are collections of failed messages grouped by a classifier (default: exception type and stack trace). Each group shows the count of failures, the first and last occurrence, and any retry operation status.")]
    public async Task<string> GetFailureGroups(
        [Description("The classifier to group by. Default is 'Exception Type and Stack Trace'")] string classifier = "Exception Type and Stack Trace",
        [Description("Optional filter for the classifier")] string? classifierFilter = null)
    {
        var results = await fetcher.GetGroups(classifier, classifierFilter);
        return JsonSerializer.Serialize(results, McpJsonOptions.Default);
    }

    [McpServerTool, Description("Get the retry history showing past retry operations and their outcomes.")]
    public async Task<string> GetRetryHistory()
    {
        var retryHistory = await retryStore.GetRetryHistory();
        return JsonSerializer.Serialize(retryHistory, McpJsonOptions.Default);
    }
}
