using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TestingTool;

/// <summary>
/// Thin HTTP client for the ServiceControl REST API. Used by the background replay and search
/// jobs to interact with the test ServiceControl instance.
/// </summary>
public sealed class ServiceControlClient(HttpClient http, ILogger<ServiceControlClient> logger)
{
    public string BaseUrl => http.BaseAddress?.ToString() ?? "(not configured)";

    /// <summary>Fetches all error groups from ServiceControl.</summary>
    public async Task<IReadOnlyList<ErrorGroup>> GetErrorGroupsAsync(CancellationToken ct = default)
    {
        try
        {
            var groups = await http.GetFromJsonAsync<List<ErrorGroup>>("/api/errors/groups", ct);
            return groups ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch error groups from ServiceControl");
            return [];
        }
    }

    /// <summary>Triggers a retry/replay of all messages in an error group.</summary>
    public async Task<bool> ReplayGroupAsync(string groupId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/api/errors/groups/{groupId}/retry", new { }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to replay error group {GroupId}", groupId);
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
        [property: JsonPropertyName("exceptionType")] string? ExceptionType,
        [property: JsonPropertyName("firstTime")] string? FirstTime,
        [property: JsonPropertyName("lastTime")] string? LastTime);

    public sealed record SearchResponse([property: JsonPropertyName("messageCount")] int MessageCount);

    public sealed record SearchResult(int MessageCount);
}