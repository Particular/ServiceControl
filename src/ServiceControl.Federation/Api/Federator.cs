namespace ServiceControl.Federation.Api;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aggregation;
using Configuration;
using Http;
using Members;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Routing;

/// <summary>
/// The reusable request strategies. Each proxied route is one of these, sometimes with a
/// route-specific merge on top (see <see cref="PrimaryRoutes"/> and <see cref="MonitoringRoutes"/>).
/// </summary>
public sealed class Federator(
    MemberRegistry registry,
    MemberGateway gateway,
    OwnerCache owners,
    OwnerResolver resolver,
    FederationSettings settings,
    ILogger<Federator> logger)
{
    public MemberRegistry Registry => registry;

    public MemberGateway Gateway => gateway;

    public OwnerCache Owners => owners;

    public FederationSettings Settings => settings;

    public ILogger Logger => logger;

    public string Prefix(Surface surface) => surface == Surface.Primary ? settings.PrimaryPathBase : settings.MonitoringPathBase;

    public IReadOnlyList<Member> Members(Surface surface) => registry.Serving(surface);

    public string? LowestVersion(Surface surface) => Versions.Lowest(Members(surface).Select(m => m.Version(surface)));

    public string Target(HttpContext context, Surface surface) => ProxyRequest.RelativeTarget(context, Prefix(surface));

    public Task<MemberResponse[]> FanOut(HttpContext context, Surface surface, string target) =>
        gateway.SendAll(Members(surface), surface, ProxyRequest.Get(context, target), context.RequestAborted);

    public Task<MemberResponse[]> FanOut(HttpContext context, Surface surface) => FanOut(context, surface, Target(context, surface));

    public ResponseMeta Meta(Surface surface, IReadOnlyCollection<MemberResponse> responses, long? totalCount = null, string variant = "", string? link = null, IEnumerable<string>? extraIncomplete = null) =>
        new()
        {
            TotalCount = totalCount,
            ETag = EntityTags.Combine(responses, variant),
            Version = LowestVersion(surface),
            Link = link,
            Incomplete = [.. ProxyResponse.IncompleteEntries(responses, surface), .. extraIncomplete ?? []]
        };

    /// <summary>
    /// Ends the request when no member produced a usable answer. A 401 or 403 is relayed as is, so the
    /// client can renew its token or explain the refusal.
    /// </summary>
    public async Task<bool> NothingAnswered(HttpContext context, Surface surface, IReadOnlyList<MemberResponse> responses, bool notFoundIsAnAnswer = true)
    {
        if (responses.Any(r => r.IsSuccess) || (notFoundIsAnAnswer && responses.Any(r => r.IsNotFound)))
        {
            return false;
        }

        var authFailure = responses.FirstOrDefault(r => r.IsAuthFailure);
        if (authFailure != null)
        {
            await ProxyResponse.Relay(context, authFailure, context.RequestAborted);
            return true;
        }

        var clientError = responses.FirstOrDefault(r => r.Failure == null && r.Status is >= 400 and < 500 and not 404);
        if (clientError != null)
        {
            await ProxyResponse.Relay(context, clientError, context.RequestAborted);
            return true;
        }

        var incomplete = ProxyResponse.IncompleteEntries(responses, surface);
        if (responses.Count == 0)
        {
            await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, $"No member serves the {surface} API.", incomplete, context.RequestAborted);
        }
        else if (responses.Any(r => r.Failure == FailureReason.Timeout))
        {
            await ProxyResponse.Problem(context, StatusCodes.Status504GatewayTimeout, "No member answered in time.", incomplete, context.RequestAborted);
        }
        else
        {
            await ProxyResponse.Problem(context, StatusCodes.Status502BadGateway, "No member answered.", incomplete, context.RequestAborted);
        }

        return true;
    }

    /// <summary>
    /// L: answered by the lead member, or by the next member when the lead is down.
    /// </summary>
    public async Task Lead(HttpContext context, Surface surface)
    {
        var request = await ProxyRequest.Repeat(context, Prefix(surface), context.RequestAborted);
        var failures = new List<MemberResponse>();
        foreach (var member in Members(surface))
        {
            var response = await gateway.Send(member, surface, request, context.RequestAborted);
            if (response.Failure == null)
            {
                await ProxyResponse.Relay(context, response, context.RequestAborted);
                return;
            }

            failures.Add(response);
        }

        await NothingAnswered(context, surface, failures);
    }

    /// <summary>
    /// L, streamed: for large or binary payloads such as license uploads.
    /// </summary>
    public async Task LeadStreaming(HttpContext context, Surface surface)
    {
        var request = await ProxyRequest.Repeat(context, Prefix(surface), context.RequestAborted);
        foreach (var member in Members(surface))
        {
            using var response = await gateway.SendStreaming(member, surface, request, context.RequestAborted);
            if (response != null)
            {
                await ProxyResponse.RelayStreaming(context, response, context.RequestAborted);
                return;
            }
        }

        await ProxyResponse.Problem(context, StatusCodes.Status502BadGateway, "No member answered.", cancellationToken: context.RequestAborted);
    }

    /// <summary>
    /// B: the command goes to every member (or the given ones); see <see cref="CommandOutcome"/>.
    /// </summary>
    public async Task Broadcast(HttpContext context, Surface surface, IReadOnlyList<Member>? targets = null)
    {
        var request = await ProxyRequest.Repeat(context, Prefix(surface), context.RequestAborted);
        var responses = await gateway.SendAll(targets ?? Members(surface), surface, request, context.RequestAborted);
        await RelayOutcome(context, surface, responses);
    }

    public async Task RelayOutcome(HttpContext context, Surface surface, IReadOnlyList<MemberResponse> responses)
    {
        var chosen = CommandOutcome.Choose(responses);
        if (chosen == null || chosen.Status == 0)
        {
            await NothingAnswered(context, surface, responses, notFoundIsAnAnswer: false);
            return;
        }

        var incomplete = ProxyResponse.IncompleteEntries(responses.Where(r => r != chosen), surface);
        if (incomplete.Count > 0)
        {
            context.Response.Headers[ProxyResponse.IncompleteResultsHeader] = string.Join(", ", incomplete);
        }

        await ProxyResponse.Relay(context, chosen, context.RequestAborted);
    }

    /// <summary>
    /// F: the first member holding the resource answers. Members are asked in parallel; the earliest
    /// member in registry order wins, so the lead member is preferred.
    /// </summary>
    public async Task FirstHit(HttpContext context, Surface surface, OwnedRecord? kind = null, bool rewriteInstanceIds = false)
    {
        var responses = await FanOut(context, surface);
        if (await NothingAnswered(context, surface, responses))
        {
            return;
        }

        var hits = responses.Where(r => r.IsSuccess).ToList();
        if (hits.Count == 0)
        {
            await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, "Not found on any member.", ProxyResponse.IncompleteEntries(responses, surface), context.RequestAborted);
            return;
        }

        foreach (var hit in hits)
        {
            if (kind is { } recordKind)
            {
                owners.Record(recordKind, JsonValues.Text(hit.Json(), "id"), hit.Member.Key);
            }
        }

        var body = hits[0].Json();
        if (rewriteInstanceIds)
        {
            InstanceIdCodec.RewriteTree(body, hits[0].Member.Key);
        }

        await ProxyResponse.WriteJson(context, body, Meta(surface, responses), context.RequestAborted);
    }

    /// <summary>
    /// S over <c>Total-Count</c>: for HEAD count endpoints.
    /// </summary>
    public async Task SumCount(HttpContext context, Surface surface)
    {
        var request = await ProxyRequest.Repeat(context, Prefix(surface), context.RequestAborted);
        var responses = await gateway.SendAll(Members(surface), surface, request, context.RequestAborted);
        if (await NothingAnswered(context, surface, responses))
        {
            return;
        }

        var total = responses.Where(r => r.IsSuccess).Sum(r => (long)(r.TotalCount ?? 0));
        await ProxyResponse.WriteJson(context, null, Meta(surface, responses, total), context.RequestAborted);
    }

    /// <summary>
    /// U: member arrays are concatenated, optionally de-duplicated by key (earliest member wins).
    /// </summary>
    public async Task Union(HttpContext context, Surface surface, Func<JsonNode, string?>? deduplicationKey = null, OwnedRecord? kind = null, string idField = "id", bool totalFromRows = false)
    {
        var responses = await FanOut(context, surface);
        if (await NothingAnswered(context, surface, responses))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new JsonArray();
        foreach (var response in responses.Where(r => r.IsSuccess))
        {
            foreach (var row in response.Array().OfType<JsonNode>())
            {
                if (kind is { } recordKind)
                {
                    owners.Record(recordKind, JsonValues.Text(row, idField), response.Member.Key);
                }

                var key = deduplicationKey?.Invoke(row);
                if (key != null && !seen.Add(key))
                {
                    continue;
                }

                result.Add(row.DeepClone());
            }
        }

        long? total = totalFromRows
            ? result.Count
            : responses.Any(r => r.TotalCount != null) ? responses.Sum(r => (long)(r.TotalCount ?? 0)) : null;

        await ProxyResponse.WriteJson(context, result, Meta(surface, responses, total), context.RequestAborted);
    }

    /// <summary>
    /// P: exact merge of sorted, paged lists (see <see cref="SortedPageMerger"/>).
    /// </summary>
    public async Task SortedPage(
        HttpContext context,
        Surface surface,
        RowOrder order,
        OwnedRecord? kind = null,
        string idField = "id",
        Func<JsonNode, string?>? deduplicationKey = null,
        Action<JsonNode, Member>? transform = null)
    {
        var page = ProxyRequest.IntQuery(context, "page", 1);
        var pageSize = ProxyRequest.IntQuery(context, "per_page", DefaultPageSize);
        var depth = (long)page * pageSize;
        var exact = depth <= settings.DeepPagingMaxRows;

        var target = exact
            ? ProxyRequest.WithQuery(context, Prefix(surface), ProxyRequest.QueryWith(context, ("page", "1"), ("per_page", depth.ToString(CultureInfo.InvariantCulture))))
            : Target(context, surface);

        var responses = await FanOut(context, surface, target);
        if (await NothingAnswered(context, surface, responses))
        {
            return;
        }

        var memberRows = responses
            .Select(response => (IReadOnlyList<JsonNode>)[.. RowsOf(response, kind, idField, transform)])
            .ToList();

        var merged = SortedPageMerger.Merge(memberRows, order, exact ? (int)(depth - pageSize) : 0, pageSize, deduplicationKey);
        var total = responses.Where(r => r.IsSuccess).Sum(r => (long)(r.TotalCount ?? 0));
        var link = ProxyResponse.PagingLinks(context, Prefix(surface), page, pageSize, total);
        string[] approximate = exact ? [] : [$"{ProxyRequest.BaseUrl(context, Prefix(surface)).TrimEnd('/')}:approximate-paging"];

        await ProxyResponse.WriteJson(
            context,
            new JsonArray([.. merged.Select(row => row.DeepClone())]),
            Meta(surface, responses, total, target, link, approximate),
            context.RequestAborted);
    }

    /// <summary>
    /// W: lists without offsets (messages2) are forwarded unchanged; the merged top N is exact.
    /// </summary>
    public async Task TopN(HttpContext context, Surface surface, RowOrder order, string pageSizeParameter, Func<JsonNode, string?>? deduplicationKey = null, Action<JsonNode, Member>? transform = null)
    {
        var responses = await FanOut(context, surface);
        if (await NothingAnswered(context, surface, responses))
        {
            return;
        }

        var take = ProxyRequest.IntQuery(context, pageSizeParameter, int.MaxValue);
        var memberRows = responses
            .Select(response => (IReadOnlyList<JsonNode>)[.. RowsOf(response, null, "id", transform)])
            .ToList();

        var merged = SortedPageMerger.Merge(memberRows, order, 0, take, deduplicationKey);
        var total = responses.Where(r => r.IsSuccess).Sum(r => (long)(r.TotalCount ?? 0));

        await ProxyResponse.WriteJson(context, new JsonArray([.. merged.Select(row => row.DeepClone())]), Meta(surface, responses, total), context.RequestAborted);
    }

    IEnumerable<JsonNode> RowsOf(MemberResponse response, OwnedRecord? kind, string idField, Action<JsonNode, Member>? transform)
    {
        if (!response.IsSuccess)
        {
            yield break;
        }

        foreach (var row in response.Array().OfType<JsonNode>())
        {
            transform?.Invoke(row, response.Member);
            if (kind is { } recordKind)
            {
                owners.Record(recordKind, JsonValues.Text(row, idField), response.Member.Key);
            }

            yield return row;
        }
    }

    /// <summary>
    /// R: the command goes only to the members holding the record.
    /// </summary>
    public async Task RouteToOwners(HttpContext context, Surface surface, OwnedRecord kind, string id)
    {
        var request = await ProxyRequest.Repeat(context, Prefix(surface), context.RequestAborted);
        var resolution = await resolver.Resolve(context, kind, [id], context.RequestAborted);
        if (resolution.Owners.Count == 0)
        {
            logger.LogWarning("No member holds {Kind} {Id}; {Method} {Path} was not forwarded", kind, id, context.Request.Method, context.Request.Path);
            await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, $"No member holds {kind} '{id}'.", cancellationToken: context.RequestAborted);
            return;
        }

        var responses = await gateway.SendAll(resolution.Owners.Keys, surface, request, context.RequestAborted);
        await RelayOutcome(context, surface, responses);
    }

    /// <summary>
    /// R, partitioned: a body listing ids (an array, or an object with <c>uniquemessageids</c>) is split
    /// so each owner receives only its ids. Any other body (a time range, for example) is broadcast.
    /// </summary>
    public async Task Partitioned(HttpContext context, Surface surface, OwnedRecord kind)
    {
        var request = await ProxyRequest.Repeat(context, Prefix(surface), context.RequestAborted);
        var body = TryParse(request.Body);
        var (ids, rebuild) = ExtractIds(body);

        if (ids == null)
        {
            var broadcast = await gateway.SendAll(Members(surface), surface, request, context.RequestAborted);
            await RelayOutcome(context, surface, broadcast);
            return;
        }

        var resolution = await resolver.Resolve(context, kind, ids, context.RequestAborted);
        if (resolution.Unresolved.Count > 0)
        {
            logger.LogWarning("No member holds {Count} of the {Kind} ids in {Method} {Path}; they were dropped: {Ids}",
                resolution.Unresolved.Count, kind, context.Request.Method, context.Request.Path, string.Join(", ", resolution.Unresolved.Take(10)));
        }

        if (resolution.Owners.Count == 0)
        {
            await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, $"No member holds any of the {kind} ids.", cancellationToken: context.RequestAborted);
            return;
        }

        var responses = await Task.WhenAll(resolution.Owners.Select(owner =>
            gateway.Send(owner.Key, surface, request with { Body = rebuild!(owner.Value), ContentType = "application/json" }, context.RequestAborted)));

        await RelayOutcome(context, surface, responses);
    }

    static JsonNode? TryParse(byte[]? body)
    {
        if (body == null || body.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static (List<string>? Ids, Func<List<string>, byte[]>? Rebuild) ExtractIds(JsonNode? body)
    {
        if (body is JsonArray array)
        {
            return ([.. array.Select(JsonValues.AsText).OfType<string>()], ids => Serialize(new JsonArray([.. ids.Select(id => (JsonNode)id)])));
        }

        if (body is JsonObject obj)
        {
            var property = obj.FirstOrDefault(p => string.Equals(p.Key, "uniquemessageids", StringComparison.OrdinalIgnoreCase));
            if (property.Value is JsonArray idArray)
            {
                byte[] Rebuild(List<string> ids)
                {
                    var copy = obj.DeepClone().AsObject();
                    copy[property.Key] = new JsonArray([.. ids.Select(id => (JsonNode)id)]);
                    return Serialize(copy);
                }

                return ([.. idArray.Select(JsonValues.AsText).OfType<string>()], Rebuild);
            }
        }

        return (null, null);
    }

    static byte[] Serialize(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    public const int DefaultPageSize = 50;
}
