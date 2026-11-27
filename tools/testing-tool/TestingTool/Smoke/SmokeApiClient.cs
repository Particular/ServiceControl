using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestingTool.Smoke;

/// <summary>
/// Strict ServiceControl primary API client for the smoke checks. Unlike <see cref="ServiceControlClient"/>
/// every transport error, timeout and unexpected status throws <see cref="SmokeCheckFailedException"/>,
/// so a check never reads an empty result as "nothing found".
/// </summary>
public sealed class SmokeApiClient(HttpClient http)
{
    // ServiceControl's JSON contract is snake_case with camelCase string enums.
    static readonly JsonSerializerOptions ServiceControlJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    const string IncompleteResultsHeader = "X-Particular-Incomplete-Results";
    const int PageSize = 100;
    const int MaxPages = 50;

    public static string UriEscape(string value) => Uri.EscapeDataString(value);

    public async Task<IReadOnlyList<RemoteInstance>> GetRemotes(CancellationToken ct) =>
        await Get<List<RemoteInstance>>("/api/configuration/remotes", ct) ?? [];

    public Task<List<CustomCheckView>> GetCustomChecks(string status, CancellationToken ct) =>
        GetAllPages<CustomCheckView>($"/api/customchecks?status={status}", ct);

    /// <summary>The RavenDB persister returns ids as <c>CustomChecks/{guid}</c>, but the delete route takes the guid.</summary>
    public Task DeleteCustomCheck(string id, CancellationToken ct) =>
        Run(HttpMethod.Delete, $"/api/customchecks/{id[(id.LastIndexOf('/') + 1)..]}", null, ct, HttpStatusCode.Accepted);

    public async Task<List<EndpointView>> GetEndpoints(CancellationToken ct) =>
        await Get<List<EndpointView>>("/api/endpoints", ct) ?? [];

    public Task DeleteEndpoint(Guid id, CancellationToken ct) => Run(HttpMethod.Delete, $"/api/endpoints/{id}", null, ct, HttpStatusCode.NoContent);

    public async Task<List<ServiceControlClient.ErrorGroup>> GetGroups(CancellationToken ct) =>
        await Get<List<ServiceControlClient.ErrorGroup>>("/api/recoverability/groups", ct) ?? [];

    public Task<List<FailedMessageView>> GetGroupErrors(string groupId, CancellationToken ct) =>
        GetAllPages<FailedMessageView>($"/api/recoverability/groups/{UriEscape(groupId)}/errors", ct);

    /// <summary>Returns null while ServiceControl answers 404, that is before the failure is ingested.</summary>
    public async Task<FailedMessageDetail?> FindFailedMessage(string id, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, $"/api/errors/{UriEscape(id)}", null, ct, HttpStatusCode.OK, HttpStatusCode.NotFound);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await Read<FailedMessageDetail>(response, ct);
    }

    public Task RetryMessage(string id, CancellationToken ct) => Run(HttpMethod.Post, $"/api/errors/{UriEscape(id)}/retry", null, ct, HttpStatusCode.Accepted);

    public Task RetryGroup(string groupId, CancellationToken ct) =>
        Run(HttpMethod.Post, $"/api/recoverability/groups/{UriEscape(groupId)}/errors/retry", null, ct, HttpStatusCode.Accepted);

    public Task ArchiveMessages(IReadOnlyList<string> ids, CancellationToken ct) => Run(HttpMethod.Patch, "/api/errors/archive", ids, ct, HttpStatusCode.Accepted);

    public async Task<bool> EditMessage(string id, string body, Dictionary<string, string> headers, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, $"/api/edit/{UriEscape(id)}", new EditRequest(body, headers), ct, HttpStatusCode.Accepted, HttpStatusCode.OK);
        return response.StatusCode == HttpStatusCode.Accepted;
    }

    public Task<List<RedirectView>> GetRedirects(CancellationToken ct) => GetAllPages<RedirectView>("/api/redirects", ct);

    public Task CreateRedirect(string from, string to, CancellationToken ct) =>
        Run(HttpMethod.Post, "/api/redirects", new RedirectRequest(from, to, false), ct, HttpStatusCode.Created);

    public Task DeleteRedirect(Guid id, CancellationToken ct) => Run(HttpMethod.Delete, $"/api/redirects/{id}", null, ct, HttpStatusCode.NoContent);

    public Task<Audited<List<AuditedMessage>>> SearchMessages(string query, CancellationToken ct) =>
        GetAudited<List<AuditedMessage>>($"/api/messages/search?q={UriEscape(query)}", ct, []);

    public Task<Audited<List<AuditedMessage>>> GetConversation(string conversationId, CancellationToken ct) =>
        GetAudited<List<AuditedMessage>>($"/api/conversations/{UriEscape(conversationId)}?per_page={PageSize}", ct, []);

    /// <summary>Follows the relative body_url of an audited message, which names the instance that stores the body.</summary>
    public async Task<string> GetMessageBody(AuditedMessage message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(message.BodyUrl))
        {
            throw new SmokeCheckFailedException($"Audited message {message.MessageId} has no body_url, so its stored body cannot be read");
        }

        using var response = await Send(HttpMethod.Get, "/api" + message.BodyUrl, null, ct, HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>The primary answers 204 until the audit instance has ingested the saga.</summary>
    public async Task<Audited<SagaHistory?>> GetSaga(Guid sagaId, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, $"/api/sagas/{sagaId}", null, ct, HttpStatusCode.OK, HttpStatusCode.NoContent);
        var incomplete = IncompleteInstances(response);
        return new Audited<SagaHistory?>(response.StatusCode == HttpStatusCode.NoContent ? null : await Read<SagaHistory>(response, ct), incomplete);
    }

    async Task<Audited<T>> GetAudited<T>(string path, CancellationToken ct, T fallback)
    {
        using var response = await Send(HttpMethod.Get, path, null, ct, HttpStatusCode.OK);
        return new Audited<T>(await Read<T>(response, ct) ?? fallback, IncompleteInstances(response));
    }

    async Task<T?> Get<T>(string path, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, path, null, ct, HttpStatusCode.OK);
        return await Read<T>(response, ct);
    }

    async Task<List<T>> GetAllPages<T>(string path, CancellationToken ct)
    {
        var all = new List<T>();
        var separator = path.Contains('?') ? '&' : '?';
        for (var page = 1; page <= MaxPages; page++)
        {
            var items = await Get<List<T>>($"{path}{separator}page={page}&per_page={PageSize}", ct) ?? [];
            all.AddRange(items);
            if (items.Count < PageSize)
            {
                return all;
            }
        }

        throw new SmokeCheckFailedException($"GET {path} has more than {MaxPages * PageSize} entries, which the smoke checks do not page through");
    }

    static string? IncompleteInstances(HttpResponseMessage response) =>
        response.Headers.TryGetValues(IncompleteResultsHeader, out var values) ? string.Join(", ", values) : null;

    static async Task<T?> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ServiceControlJson, ct);
        }
        catch (JsonException ex)
        {
            throw new SmokeCheckFailedException($"{response.RequestMessage?.RequestUri?.PathAndQuery} returned a body that is not the expected JSON: {ex.Message}");
        }
    }

    async Task Run(HttpMethod method, string path, object? body, CancellationToken ct, params HttpStatusCode[] accepted)
    {
        using var response = await Send(method, path, body, ct, accepted);
    }

    Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, CancellationToken ct, params HttpStatusCode[] accepted) =>
        SmokeHttp.Send(http, method, path, body is null ? null : JsonContent.Create(body, body.GetType(), null, ServiceControlJson), ct, accepted);

    public sealed record RemoteInstance(string ApiUri, string Status);

    public sealed record CustomCheckView(string Id, string CustomCheckId, string Status, string? FailureReason, DateTime ReportedAt);

    public sealed record EndpointView(Guid Id, string Name, bool MonitorHeartbeat, HeartbeatInformation? HeartbeatInformation);

    public sealed record HeartbeatInformation(string ReportedStatus);

    public sealed record FailedMessageView(string Id, string MessageId, string Status, string? QueueAddress, bool Edited, string? EditOf);

    public sealed record FailedMessageDetail(string Status, List<ProcessingAttempt> ProcessingAttempts);

    public sealed record ProcessingAttempt(Dictionary<string, string> Headers);

    public sealed record RedirectView(Guid MessageRedirectId, string FromPhysicalAddress, string ToPhysicalAddress);

    public sealed record AuditedMessage(string Id, string MessageId, string MessageType, string Status, string? ConversationId, string? BodyUrl, List<HeaderEntry>? Headers, List<SagaReference>? InvokedSagas)
    {
        public string? Header(string name) => Headers?.FirstOrDefault(h => h.Key == name)?.Value;
    }

    public sealed record HeaderEntry(string Key, string? Value);

    public sealed record SagaReference(Guid SagaId, string SagaType);

    public sealed record SagaHistory(Guid SagaId, string SagaType, List<SagaChange> Changes);

    public sealed record SagaChange(string Status, DateTime FinishTime);

    /// <summary>A read that spans instances. <see cref="Incomplete"/> names the instances that did not answer.</summary>
    public sealed record Audited<T>(T Value, string? Incomplete);

    sealed record EditRequest(string MessageBody, Dictionary<string, string> MessageHeaders);

    sealed record RedirectRequest(
        [property: JsonPropertyName("fromphysicaladdress")] string From,
        [property: JsonPropertyName("tophysicaladdress")] string To,
        [property: JsonPropertyName("retryexisting")] bool RetryExisting);
}

/// <summary>
/// ServiceControl.Monitoring API client for the smoke checks. Routes have no <c>/api</c> prefix and
/// the JSON is camelCase.
/// </summary>
public sealed class SmokeMonitoringClient(HttpClient http)
{
    static readonly JsonSerializerOptions MonitoringJson = new(JsonSerializerDefaults.Web);

    public async Task<List<MonitoredEndpoint>> GetMonitoredEndpoints(CancellationToken ct)
    {
        using var response = await SmokeHttp.Send(http, HttpMethod.Get, "/monitored-endpoints", null, ct, HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<List<MonitoredEndpoint>>(MonitoringJson, ct) ?? [];
    }

    public async Task DeleteInstance(string endpointName, string instanceId, CancellationToken ct)
    {
        using var response = await SmokeHttp.Send(http, HttpMethod.Delete,
            $"/monitored-instance/{Uri.EscapeDataString(endpointName)}/{Uri.EscapeDataString(instanceId)}", null, ct, HttpStatusCode.OK);
    }

    public sealed record MonitoredEndpoint(string Name, bool IsStale, string[]? EndpointInstanceIds, Dictionary<string, MonitoredValues> Metrics);

    public sealed record MonitoredValues(double? Average);
}

static class SmokeHttp
{
    public static async Task<HttpResponseMessage> Send(HttpClient http, HttpMethod method, string path, HttpContent? content, CancellationToken ct, params HttpStatusCode[] accepted)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new SmokeCheckFailedException($"{method} {path} could not reach {http.BaseAddress}: {ex.Message}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SmokeCheckFailedException($"{method} {path} to {http.BaseAddress} timed out");
        }

        if (!accepted.Contains(response.StatusCode))
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new SmokeCheckFailedException($"{method} {path} returned {status}: {(text.Length > 300 ? text[..300] : text)}");
        }

        return response;
    }
}
