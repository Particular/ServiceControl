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
    /// Triggers a manual retention purge on the ServiceControl error instance
    /// (<c>POST /api/maintenance/retention/purge</c>). The delete work runs in the background on
    /// ServiceControl; this returns as soon as the run is accepted (or refused because one is
    /// already running / unsupported by the persister). A null request — or null cutoffs within
    /// it — leaves ServiceControl to derive the cutoffs from its configured retention periods,
    /// just as the scheduled hourly sweep does.
    /// </summary>
    /// <returns>The outcome: <c>Status</c> is <c>started</c> (202), <c>alreadyRunning</c> (409),
    /// <c>notSupported</c> (501) or <c>error</c> (400, e.g. an invalid cutoff); <c>null</c> on a
    /// transport/HTTP failure.</returns>
    public async Task<RetentionPurgeResponse?> PurgeRetentionAsync(RetentionPurgeRequest? request, CancellationToken ct = default)
    {
        try
        {
            // ServiceControl's JSON contract is snake_case with camelCase string enums, so the
            // request/response records below spell out the property names explicitly to match it.
            var response = await http.PostAsJsonAsync("/api/maintenance/retention/purge", request ?? new RetentionPurgeRequest(null, null), ct);

            // Both success (202) and refusal (409/501/400) carry a JSON body describing the
            // outcome — deserialize it either way so the caller can react to the status string.
            return await response.Content.ReadFromJsonAsync<RetentionPurgeResponse>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to trigger retention purge on ServiceControl");
            return null;
        }
    }

    /// <summary>
    /// Polls the execution state of the most recent retention purge. On a persister with no
    /// sweeper (e.g. RavenDB, which uses server-side document expiration) ServiceControl
    /// returns 501 with a <see cref="RetentionPurgeStatusResponse.Reason"/>.
    /// </summary>
    public async Task<RetentionPurgeStatusResponse?> GetRetentionPurgeStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync("/api/maintenance/retention/purge/status", ct);
            return await response.Content.ReadFromJsonAsync<RetentionPurgeStatusResponse>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch retention purge status from ServiceControl");
            return null;
        }
    }

    /// <summary>
    /// Cutoffs for a manual retention purge, mirroring ServiceControl's retention-purge request
    /// contract. Both are optional — a null cutoff makes ServiceControl use the corresponding
    /// configured retention period. Supplied cutoffs must be UTC and in the past; anything else
    /// is refused with HTTP 400.
    /// </summary>
    public sealed record RetentionPurgeRequest(
        [property: JsonPropertyName("error_cutoff")] DateTime? ErrorCutoff,
        [property: JsonPropertyName("events_cutoff")] DateTime? EventsCutoff);

    /// <summary>
    /// Outcome of a manual retention purge, mirroring ServiceControl's retention-purge response
    /// contract. <see cref="Status"/> is one of <c>started</c> (202), <c>alreadyRunning</c> (409),
    /// <c>notSupported</c> (501) or <c>error</c> (400); <see cref="Reason"/> accompanies the
    /// refusals.
    /// </summary>
    public sealed record RetentionPurgeResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("started_at")] DateTime? StartedAt,
        [property: JsonPropertyName("error_cutoff")] DateTime? ErrorCutoff,
        [property: JsonPropertyName("events_cutoff")] DateTime? EventsCutoff,
        [property: JsonPropertyName("reason")] string? Reason);

    /// <summary>
    /// Execution state of the most recent retention purge, mirroring ServiceControl's
    /// retention-purge status contract. <see cref="Reason"/> is only present on the 501 response
    /// returned by persisters with no sweeper (e.g. RavenDB).
    /// </summary>
    public sealed record RetentionPurgeStatusResponse(
        [property: JsonPropertyName("is_running")] bool IsRunning,
        [property: JsonPropertyName("last_started_at")] DateTime? LastStartedAt,
        [property: JsonPropertyName("last_finished_at")] DateTime? LastFinishedAt,
        [property: JsonPropertyName("last_error_cutoff")] DateTime? LastErrorCutoff,
        [property: JsonPropertyName("last_events_cutoff")] DateTime? LastEventsCutoff,
        [property: JsonPropertyName("reason")] string? Reason);
}