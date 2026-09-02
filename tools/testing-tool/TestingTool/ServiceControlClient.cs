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

    /// <summary>
    /// Triggers a manual retention sweep on the ServiceControl error instance. The delete work
    /// runs in the background on ServiceControl; this returns as soon as the run is accepted
    /// (or refused because one is already running / unsupported by the persister). Both cutoffs
    /// are omitted so ServiceControl derives them from the configured retention periods, just
    /// as the scheduled hourly sweep does.
    /// </summary>
    /// <returns>The response status: <c>started</c>, <c>already-running</c>, <c>maintenance</c>,
    /// <c>not-supported</c>, or <c>invalid-cutoff</c>; <c>null</c> on a transport/HTTP failure.</returns>
    public async Task<RetentionSweepResponse?> SweepRetentionAsync(CancellationToken ct = default)
    {
        try
        {
            // No body — both cutoffs default to (now - retention period) inside ServiceControl.
            var response = await http.PostAsJsonAsync("/api/retention/sweep", new { }, ct);

            // Both success (202) and refusal (409/501/503/400) carry a JSON body describing the
            // outcome — deserialize it either way so the caller can react to the status string.
            return await response.Content.ReadFromJsonAsync<RetentionSweepResponse>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to trigger retention sweep on ServiceControl");
            return null;
        }
    }

    /// <summary>
    /// Polls the execution state of the most recent retention sweep. On a persister with no
    /// sweeper (e.g. RavenDB, which uses server-side document expiration) ServiceControl
    /// returns 501 with a <see cref="RetentionSweepStatus.Reason"/>.
    /// </summary>
    public async Task<RetentionSweepStatus?> GetRetentionSweepStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync("/api/retention/sweep/status", ct);
            return await response.Content.ReadFromJsonAsync<RetentionSweepStatus>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch retention sweep status from ServiceControl");
            return null;
        }
    }

    public sealed record RetentionSweepResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("startedAt")] DateTime? StartedAt,
        [property: JsonPropertyName("errorCutoff")] DateTime? ErrorCutoff,
        [property: JsonPropertyName("eventsCutoff")] DateTime? EventsCutoff,
        [property: JsonPropertyName("reason")] string? Reason);

    public sealed record RetentionSweepStatus(
        [property: JsonPropertyName("isRunning")] bool IsRunning,
        [property: JsonPropertyName("lastStartedAt")] DateTime? LastStartedAt,
        [property: JsonPropertyName("lastFinishedAt")] DateTime? LastFinishedAt,
        [property: JsonPropertyName("lastErrorCutoff")] DateTime? LastErrorCutoff,
        [property: JsonPropertyName("lastEventsCutoff")] DateTime? LastEventsCutoff,
        [property: JsonPropertyName("lastError")] string? LastError,
        [property: JsonPropertyName("reason")] string? Reason);
}