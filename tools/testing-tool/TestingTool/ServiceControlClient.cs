using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TestingTool;

/// <summary>
/// Thin HTTP client for the ServiceControl REST API. Used by the recoverability jobs (retry,
/// archive) and the search job to interact with the test ServiceControl instance.
/// </summary>
public sealed class ServiceControlClient(HttpClient http, ILogger<ServiceControlClient> logger)
{
    public string BaseUrl => http.BaseAddress?.ToString() ?? "(not configured)";

    /// <summary>
    /// Fetches the active error (failure) groups from ServiceControl. These are the recoverability
    /// groups that the retry and archive jobs operate on.
    /// </summary>
    public async Task<IReadOnlyList<ErrorGroup>> GetErrorGroupsAsync(CancellationToken ct = default)
    {
        try
        {
            // ServiceControl exposes failure groups under /api/recoverability/groups. The response
            // entries are GroupOperation objects (id, title, count, type, ...).
            var groups = await http.GetFromJsonAsync<List<ErrorGroup>>("/api/recoverability/groups", ct);
            return groups ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch error groups from ServiceControl");
            return [];
        }
    }

    /// <summary>Triggers a retry of all messages in an error group (async, 202 Accepted on success).</summary>
    public async Task<bool> RetryGroupAsync(string groupId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/api/recoverability/groups/{groupId}/errors/retry", new { }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retry error group {GroupId}", groupId);
            return false;
        }
    }

    /// <summary>Triggers an archive of all messages in an error group (async, 202 Accepted on success).</summary>
    public async Task<bool> ArchiveGroupAsync(string groupId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/api/recoverability/groups/{groupId}/errors/archive", new { }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to archive error group {GroupId}", groupId);
            return false;
        }
    }

    /// <summary>Executes a full-text search query against ServiceControl.</summary>
    public async Task<SearchResult?> SearchAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync($"/api/errors/search?q={Uri.EscapeDataString(query)}", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<SearchResponse>(ct);
            return new SearchResult(body?.MessageCount ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Search '{Query}' failed", query);
            return null;
        }
    }

    public sealed record ErrorGroup(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("first")] string? First,
        [property: JsonPropertyName("last")] string? Last);

    public sealed record SearchResponse([property: JsonPropertyName("messageCount")] int MessageCount);

    public sealed record SearchResult(int MessageCount);
}