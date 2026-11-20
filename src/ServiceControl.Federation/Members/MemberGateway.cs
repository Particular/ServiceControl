namespace ServiceControl.Federation.Members;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sends requests to members and classifies the outcome the same way ServiceControl's own
/// scatter-gather does: connection failures mark the member unavailable, timeouts and 504s are
/// timeouts, and every other non-success status is passed back for the caller to judge.
/// </summary>
public sealed class MemberGateway(IHttpClientFactory httpClientFactory, FederationSettings settings, ILogger<MemberGateway> logger)
{
    public const string HttpClientName = "federation-members";

    public Task<MemberResponse[]> SendAll(IEnumerable<Member> members, Surface surface, MemberRequest request, CancellationToken cancellationToken = default) =>
        Task.WhenAll(members.Select(member => Send(member, surface, request, cancellationToken)));

    public async Task<MemberResponse> Send(Member member, Surface surface, MemberRequest request, CancellationToken cancellationToken = default)
    {
        var baseUri = member.BaseUri(surface);
        if (baseUri == null)
        {
            return MemberResponse.Failed(member, FailureReason.Unavailable);
        }

        if (member.IsUnavailable(surface))
        {
            return MemberResponse.Failed(member, FailureReason.Unavailable);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.MemberTimeout);

        try
        {
            using var message = CreateMessage(baseUri, request);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, timeout.Token);
            var body = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            var headers = CollectHeaders(response);
            member.RecordVersion(surface, response.Headers.TryGetValues("X-Particular-Version", out var versions) ? versions.FirstOrDefault() : null);

            var status = (int)response.StatusCode;
            FailureReason? failure = status == 504 ? FailureReason.Timeout : null;
            if (status is >= 500 and not 504)
            {
                failure = FailureReason.Error;
            }

            if (failure != null)
            {
                logger.LogWarning("Member {Member} answered {Status} for {Method} {Path}", member.Key, status, request.Method, request.PathAndQuery);
            }

            return new MemberResponse(member, status, headers, body, failure);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Member {Member} is unreachable at {BaseUri}; skipping it until the health probe succeeds", member.Key, baseUri);
            member.MarkUnavailable(surface, true);
            return MemberResponse.Failed(member, FailureReason.Unavailable);
        }
        catch (OperationCanceledException) when (timeout.Token.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            // Intentional: the per-member timeout fired
            logger.LogWarning("Member {Member} timed out after {Timeout} for {Method} {Path}", member.Key, settings.MemberTimeout, request.Method, request.PathAndQuery);
            return MemberResponse.Failed(member, FailureReason.Timeout);
        }
    }

    /// <summary>
    /// Sends a request whose response is streamed to the caller rather than buffered. The caller owns
    /// the returned response. Returns null when the member could not be reached.
    /// </summary>
    public async Task<HttpResponseMessage?> SendStreaming(Member member, Surface surface, MemberRequest request, CancellationToken cancellationToken = default)
    {
        var baseUri = member.BaseUri(surface);
        if (baseUri == null || member.IsUnavailable(surface))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.TransferTimeout);

        try
        {
            using var message = CreateMessage(baseUri, request);
            return await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Member {Member} is unreachable at {BaseUri}", member.Key, baseUri);
            member.MarkUnavailable(surface, true);
            return null;
        }
        catch (OperationCanceledException) when (timeout.Token.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            // Intentional: the transfer timeout fired
            logger.LogWarning("Member {Member} timed out streaming {Method} {Path}", member.Key, request.Method, request.PathAndQuery);
            return null;
        }
    }

    /// <summary>
    /// Health probe: bypasses the unavailable flag and resets it on success.
    /// </summary>
    public async Task<bool> Probe(Member member, Surface surface, CancellationToken cancellationToken = default)
    {
        var baseUri = member.BaseUri(surface);
        if (baseUri == null)
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.MemberTimeout);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, baseUri);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, timeout.Token);
            member.RecordVersion(surface, response.Headers.TryGetValues("X-Particular-Version", out var versions) ? versions.FirstOrDefault() : null);
            // Any HTTP answer means the member is reachable. Requests then classify their own responses;
            // only connection failures and timeouts take a member out of rotation.
            if (member.IsUnavailable(surface))
            {
                logger.LogInformation("Member {Member} {Surface} is reachable again", member.Key, surface);
            }

            member.MarkUnavailable(surface, false);
            return true;
        }
        catch (OperationCanceledException) when (timeout.Token.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            // Intentional: the probe timed out
            return Unreachable(member, surface, baseUri);
        }
        catch (HttpRequestException)
        {
            return Unreachable(member, surface, baseUri);
        }
    }

    bool Unreachable(Member member, Surface surface, Uri baseUri)
    {
        if (!member.IsUnavailable(surface))
        {
            logger.LogWarning("Member {Member} {Surface} at {BaseUri} is unreachable", member.Key, surface, baseUri);
        }

        member.MarkUnavailable(surface, true);
        return false;
    }

    static HttpRequestMessage CreateMessage(Uri baseUri, MemberRequest request)
    {
        var message = new HttpRequestMessage(request.Method, new Uri(baseUri, request.PathAndQuery));
        message.Headers.Accept.ParseAdd(request.Accept ?? "application/json");

        if (!string.IsNullOrEmpty(request.Authorization))
        {
            message.Headers.TryAddWithoutValidation("Authorization", request.Authorization);
        }

        if (request.Body != null)
        {
            message.Content = new ByteArrayContent(request.Body);
            if (!string.IsNullOrEmpty(request.ContentType))
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
            }
        }

        return message;
    }

    static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        return headers;
    }
}
