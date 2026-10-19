namespace ServiceControl.Federation.Api;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aggregation;
using Http;
using Members;
using Microsoft.AspNetCore.Http;
using Routing;

/// <summary>
/// Route-specific merges for the primary API, on top of the strategies in <see cref="Federator"/>.
/// </summary>
static class PrimaryHandlers
{
    const Surface Primary = Surface.Primary;

    /// <summary>
    /// The root document: the lead member's, with every absolute URL pointing at the proxy.
    /// </summary>
    public static async Task Root(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary, "");
        if (await f.NothingAnswered(context, Primary, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).ToList();
        var source = successes[0];
        var body = source.Json()!.DeepClone().AsObject();
        var memberBase = source.Member.PrimaryUri.ToString();
        var proxyBase = ProxyRequest.BaseUrl(context, f.Prefix(Primary));

        foreach (var property in body.ToList())
        {
            if (JsonValues.AsText(property.Value) is { } text && text.StartsWith(memberBase, StringComparison.OrdinalIgnoreCase))
            {
                body[property.Key] = proxyBase + text[memberBase.Length..];
            }
        }

        body["name"] = f.Settings.DisplayName;

        var invalidLicense = successes.Select(r => JsonValues.Text(r.Json(), "license_status")).FirstOrDefault(s => s is not null and not "valid");
        if (invalidLicense != null)
        {
            body["license_status"] = invalidLicense;
        }

        // The other member primaries have their own rows (configuration/remotes), so this row describes only the member that answered.
        DescribeSource(body, source);

        await ProxyResponse.WriteJson(context, body, new ResponseMeta { Version = f.LowestVersion(Primary) }, context.RequestAborted);
    }

    /// <summary>
    /// ServicePulse shows a root document's <c>platform_health_version</c> in preference to
    /// <c>X-Particular-Version</c>, while its feature gates read only the header. The body therefore
    /// carries the answering member's own version, and the header keeps the lowest member version.
    /// </summary>
    public static void DescribeSource(JsonObject body, MemberResponse source) =>
        body["platform_health_version"] = JsonValues.Text(source.Json(), "platform_health_version") ?? source.Version ?? JsonValues.Text(source.Json(), "version");

    /// <summary>
    /// The worst health any member reports, and at least degraded when a member does not answer.
    /// </summary>
    public static string WorstHealth(IReadOnlyList<MemberResponse> responses)
    {
        var worst = responses
            .Where(r => r.IsSuccess)
            .Select(r => JsonValues.Text(r.Json(), "platform_health_status") ?? "healthy")
            .DefaultIfEmpty("healthy")
            .MaxBy(HealthRank)!;

        return (responses.Any(r => !r.IsSuccess) && HealthRank(worst) < HealthRank("degraded")) ? "degraded" : worst;
    }

    static int HealthRank(string status) => status switch
    {
        "healthy" => 0,
        "degraded" => 1,
        "unavailable" => 2,
        _ => 1
    };

    /// <summary>
    /// <c>configuration</c> and <c>instance-info</c>: the lead member's document, with the shortest
    /// retention periods, since ServicePulse uses them to warn about data scheduled for deletion.
    /// </summary>
    public static async Task Configuration(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).Select(r => r.Json()).ToList();
        var body = successes[0]!.DeepClone().AsObject();

        if (body["data_retention"] is JsonObject retention)
        {
            foreach (var field in new[] { "error_retention_period", "audit_retention_period" })
            {
                var shortest = successes
                    .Select(s => JsonValues.Text(s, $"data_retention.{field}"))
                    .Where(v => v != null && TimeSpan.TryParse(v, CultureInfo.InvariantCulture, out _))
                    .MinBy(v => TimeSpan.Parse(v!, CultureInfo.InvariantCulture));
                if (shortest != null)
                {
                    retention[field] = shortest;
                }
            }
        }

        if (body["host"] is JsonObject host)
        {
            host["instance_name"] = f.Settings.DisplayName;
        }

        await ProxyResponse.WriteJson(context, body, new ResponseMeta { Version = f.LowestVersion(Primary) }, context.RequestAborted);
    }

    /// <summary>
    /// The members' audit instances, plus every non-lead member primary as a remote error instance,
    /// which ServicePulse already shows in platform health.
    /// </summary>
    public static async Task Remotes(Federator f, HttpContext context)
    {
        var members = f.Members(Primary);
        var remotesTask = f.FanOut(context, Primary, "configuration/remotes");
        var configurationsTask = f.Gateway.SendAll(members.Skip(1), Primary, ProxyRequest.Get(context, "configuration"), context.RequestAborted);
        var remotes = await remotesTask;
        var configurations = await configurationsTask;

        var result = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var remote in remotes.Where(r => r.IsSuccess).SelectMany(r => r.Array().OfType<JsonNode>()))
        {
            if (seen.Add(JsonValues.Text(remote, "api_uri") ?? remote.ToJsonString()))
            {
                result.Add(remote.DeepClone());
            }
        }

        foreach (var configuration in configurations)
        {
            var member = configuration.Member;
            var entry = new JsonObject
            {
                ["api_uri"] = member.PrimaryUri.ToString(),
                ["version"] = member.Version(Primary) ?? "Unknown",
                ["status"] = configuration.IsSuccess ? "online" : "unavailable"
            };
            if (configuration.IsSuccess)
            {
                entry["configuration"] = configuration.Json()?.DeepClone();
            }

            result.Add(entry);
        }

        await ProxyResponse.WriteJson(context, result, new ResponseMeta { Version = f.LowestVersion(Primary) }, context.RequestAborted);
    }

    /// <summary>
    /// The routes the caller may invoke: those allowed on every member that answered, because
    /// broadcast commands reach every member.
    /// </summary>
    public static Task MyRoutes(Federator f, HttpContext context) => MyRoutes(f, context, Primary, "my/routes");

    public static async Task MyRoutes(Federator f, HttpContext context, Surface surface, string memberTarget)
    {
        var responses = await f.FanOut(context, surface, memberTarget);
        if (await f.NothingAnswered(context, surface, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).Select(r => r.Json()).ToList();
        var allowedEverywhere = successes
            .Select(s => (JsonValues.At(s, "routes") as JsonArray ?? []).Select(RouteKey).ToHashSet(StringComparer.Ordinal))
            .Aggregate((a, b) =>
            {
                a.IntersectWith(b);
                return a;
            });

        var routes = new JsonArray([.. (JsonValues.At(successes[0], "routes") as JsonArray ?? [])
            .Where(route => allowedEverywhere.Contains(RouteKey(route)))
            .Select(route => route?.DeepClone())]);

        var roles = new JsonArray([.. successes
            .SelectMany(s => JsonValues.At(s, "roles") as JsonArray ?? [])
            .Select(JsonValues.AsText)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(role => (JsonNode)role)]);

        await ProxyResponse.WriteJson(context, new JsonObject { ["roles"] = roles, ["routes"] = routes }, new ResponseMeta { Version = f.LowestVersion(surface) }, context.RequestAborted);
    }

    static string RouteKey(JsonNode? route)
    {
        var method = (JsonValues.Text(route, "method") ?? "").ToUpperInvariant();
        var template = JsonValues.Text(route, "url_template") ?? "";
        var normalised = string.Join('/', template.Trim('/').Split('/').Select(s => s.StartsWith('{') ? "{}" : s.ToLowerInvariant()));
        return $"{method} /{normalised}";
    }

    /// <summary>
    /// The license of the member in the worst state, so an expiring or invalid license anywhere is visible.
    /// </summary>
    public static Task License(Federator f, HttpContext context) => License(f, context, Primary);

    public static async Task License(Federator f, HttpContext context, Surface surface)
    {
        var responses = await f.FanOut(context, surface);
        if (await f.NothingAnswered(context, surface, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).Select(r => r.Json()).ToList();
        var worst = successes.FirstOrDefault(s => string.Equals(JsonValues.Text(s, "status"), "invalid", StringComparison.OrdinalIgnoreCase))
            ?? successes.FirstOrDefault(s => JsonValues.Text(s, "license_status") is { } status && !string.Equals(status, "Valid", StringComparison.OrdinalIgnoreCase))
            ?? successes[0];

        var body = worst!.DeepClone().AsObject();
        if (body.ContainsKey("instance_name"))
        {
            body["instance_name"] = f.Settings.DisplayName;
        }

        await ProxyResponse.WriteJson(context, body, new ResponseMeta { Version = f.LowestVersion(surface) }, context.RequestAborted);
    }

    /// <summary>
    /// Failed message lists: exact sorted page merge, recording owners for later commands.
    /// </summary>
    public static Task FailedMessages(Federator f, HttpContext context) =>
        f.SortedPage(
            context,
            Primary,
            RowOrder.FailedMessages(ProxyRequest.Query(context, "sort"), ProxyRequest.Query(context, "direction")),
            OwnedRecord.FailedMessage);

    /// <summary>
    /// Audit and composite message lists. Rows are de-duplicated as ServiceControl does, and every
    /// <c>instance_id</c> is made routable back to its member.
    /// </summary>
    public static Task Messages(Federator f, HttpContext context) =>
        f.SortedPage(
            context,
            Primary,
            RowOrder.Messages(ProxyRequest.Query(context, "sort"), ProxyRequest.Query(context, "direction")),
            deduplicationKey: MessageKey,
            transform: (row, member) => InstanceIdCodec.RewriteTree(row, member.Key));

    public static Task Messages2(Federator f, HttpContext context) =>
        f.TopN(
            context,
            Primary,
            RowOrder.Messages(ProxyRequest.Query(context, "sort"), ProxyRequest.Query(context, "direction")),
            "page_size",
            MessageKey,
            (row, member) => InstanceIdCodec.RewriteTree(row, member.Key));

    static string? MessageKey(JsonNode row) => $"{JsonValues.Text(row, "receiving_endpoint.name")}-{JsonValues.Text(row, "message_id")}";

    public static Task CustomChecks(Federator f, HttpContext context) =>
        f.SortedPage(context, Primary, RowOrder.CustomChecks(), OwnedRecord.CustomCheck);

    /// <summary>
    /// EF Core persisters number event log items 1, 2, 3… on every member, so ids are prefixed with
    /// the member key. No API accepts an event log id, so nothing needs to decode it.
    /// </summary>
    public static Task EventLog(Federator f, HttpContext context) =>
        f.SortedPage(
            context,
            Primary,
            RowOrder.EventLog(),
            transform: (row, member) =>
            {
                if (row is JsonObject obj && JsonValues.Text(obj, "id") is { } id)
                {
                    obj["id"] = InstanceIdCodec.Encode(member.Key, id);
                }
            });

    public static Task Redirects(Federator f, HttpContext context) =>
        f.SortedPage(
            context,
            Primary,
            RowOrder.Redirects(ProxyRequest.Query(context, "sort"), ProxyRequest.Query(context, "direction")),
            OwnedRecord.Redirect,
            "message_redirect_id");

    public static Task Endpoints(Federator f, HttpContext context) => f.Union(context, Primary, kind: OwnedRecord.Endpoint);

    public static Task KnownEndpoints(Federator f, HttpContext context) =>
        f.Union(context, Primary, row => JsonValues.Text(row, "id"), OwnedRecord.Endpoint, totalFromRows: true);

    public static Task EndpointSettings(Federator f, HttpContext context) => f.Union(context, Primary, row => JsonValues.Text(row, "name") ?? "");

    public static Task Classifiers(Federator f, HttpContext context) => f.Union(context, Primary, JsonValues.AsText);

    /// <summary>
    /// Sums every numeric property; used for <c>heartbeats/stats</c>.
    /// </summary>
    public static async Task SumNumbers(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var result = new JsonObject();
        foreach (var body in responses.Where(r => r.IsSuccess).Select(r => r.Json()).OfType<JsonObject>())
        {
            foreach (var (name, value) in body)
            {
                var number = JsonValues.AsNumber(value);
                if (number != null && JsonValues.AsNumber(result[name]) is { } existing)
                {
                    result[name] = existing + number.Value;
                }
                else if (!result.ContainsKey(name))
                {
                    result[name] = value?.DeepClone();
                }
            }
        }

        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses, responses.Where(r => r.IsSuccess).Sum(r => (long)(r.TotalCount ?? 0))), context.RequestAborted);
    }

    /// <summary>
    /// <c>errors/summary</c>: <c>{ category: { name: count } }</c>, summed per name.
    /// </summary>
    public static async Task ErrorsSummary(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var result = new JsonObject();
        foreach (var body in responses.Where(r => r.IsSuccess).Select(r => r.Json()).OfType<JsonObject>())
        {
            foreach (var (category, counts) in body)
            {
                if (counts is not JsonObject countsByName)
                {
                    continue;
                }

                if (result[category] is not JsonObject target)
                {
                    target = [];
                    result[category] = target;
                }

                foreach (var (name, count) in countsByName)
                {
                    target[name] = (long)((JsonValues.AsNumber(target[name]) ?? 0) + (JsonValues.AsNumber(count) ?? 0));
                }
            }
        }

        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Failed message counts per queue, summed per address and paged by the proxy.
    /// </summary>
    public static async Task QueueAddresses(Federator f, HttpContext context)
    {
        var maxRows = f.Settings.DeepPagingMaxRows.ToString(CultureInfo.InvariantCulture);
        var target = ProxyRequest.WithQuery(context, f.Prefix(Primary), ProxyRequest.QueryWith(context, ("page", "1"), ("per_page", maxRows)));
        var responses = await f.FanOut(context, Primary, target);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var merged = responses
            .Where(r => r.IsSuccess)
            .SelectMany(r => r.Array().OfType<JsonNode>())
            .GroupBy(row => JsonValues.Text(row, "physical_address") ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var row = group.First().DeepClone().AsObject();
                row["failed_message_count"] = group.Sum(r => (long)(JsonValues.Number(r, "failed_message_count") ?? 0));
                return row;
            })
            .OrderBy(row => JsonValues.Text(row, "physical_address"), StringComparer.Ordinal)
            .ToList();

        var page = ProxyRequest.IntQuery(context, "page", 1);
        var pageSize = ProxyRequest.IntQuery(context, "per_page", Federator.DefaultPageSize);
        var rows = new JsonArray([.. merged.Skip((page - 1) * pageSize).Take(pageSize)]);
        var link = ProxyResponse.PagingLinks(context, f.Prefix(Primary), page, pageSize, merged.Count);

        await ProxyResponse.WriteJson(context, rows, f.Meta(Primary, responses, merged.Count, target, link), context.RequestAborted);
    }

    public static async Task AuditCounts(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var merged = responses
            .Where(r => r.IsSuccess)
            .SelectMany(r => r.Array().OfType<JsonNode>())
            .GroupBy(row => JsonValues.Text(row, "utc_date") ?? "", StringComparer.Ordinal)
            .Select(group =>
            {
                var row = group.First().DeepClone().AsObject();
                row["count"] = group.Sum(r => (long)(JsonValues.Number(r, "count") ?? 0));
                return row;
            })
            .OrderBy(row => JsonValues.TimeTicks(row, "utc_date") ?? 0);

        await ProxyResponse.WriteJson(context, new JsonArray([.. merged]), f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Unresolved and archived failure group lists, merged by group id.
    /// </summary>
    public static async Task Groups(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var merged = FailureGroupMerger.MergeLists([.. responses.Select(r => (IReadOnlyList<JsonNode>)[.. r.Array().OfType<JsonNode>()])]);
        await ProxyResponse.WriteJson(context, merged, f.Meta(Primary, responses, merged.Count), context.RequestAborted);
    }

    public static async Task GroupById(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var hits = responses.Where(r => r.IsSuccess).Select(r => r.Json()).OfType<JsonNode>().ToList();
        if (hits.Count == 0)
        {
            await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, "No member holds this group.", cancellationToken: context.RequestAborted);
            return;
        }

        await ProxyResponse.WriteJson(context, FailureGroupMerger.Merge(hits), f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Retry history: operations from every member, newest first, keeping one member's depth.
    /// </summary>
    public static async Task History(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var bodies = responses.Where(r => r.IsSuccess).Select(r => r.Json()).OfType<JsonObject>().ToList();
        if (bodies.Count == 0)
        {
            await ProxyResponse.WriteJson(context, new JsonObject(), f.Meta(Primary, responses), context.RequestAborted);
            return;
        }

        var result = bodies[0].DeepClone().AsObject();
        var historic = bodies.Select(b => b["historic_operations"] as JsonArray ?? []).ToList();
        var depth = historic.Max(h => h.Count);
        result["historic_operations"] = new JsonArray([.. historic
            .SelectMany(h => h.OfType<JsonNode>())
            .OrderByDescending(op => JsonValues.TimeTicks(op, "completion_time") ?? long.MinValue)
            .Take(depth)
            .Select(op => op.DeepClone())]);
        result["unacknowledged_operations"] = new JsonArray([.. bodies
            .SelectMany(b => b["unacknowledged_operations"] as JsonArray ?? [])
            .OfType<JsonNode>()
            .Select(op => op.DeepClone())]);

        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Saga history may be spread over members; changes are merged newest first, as
    /// <c>GetSagaByIdApi</c> does across audit instances.
    /// </summary>
    public static async Task Saga(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var hits = responses.Where(r => r.IsSuccess && r.Json() != null).ToList();
        if (hits.Count == 0)
        {
            await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, "No member holds this saga.", cancellationToken: context.RequestAborted);
            return;
        }

        foreach (var hit in hits)
        {
            InstanceIdCodec.RewriteTree(hit.Json(), hit.Member.Key);
        }

        var result = hits[0].Json()!.DeepClone().AsObject();
        if (hits.Count > 1)
        {
            result["changes"] = new JsonArray([.. hits
                .SelectMany(h => JsonValues.At(h.Json(), "changes") as JsonArray ?? [])
                .OfType<JsonNode>()
                .OrderByDescending(c => JsonValues.TimeTicks(c, "finish_time") ?? long.MinValue)
                .Select(c => c.DeepClone())]);
        }

        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Message bodies are routed by the member key in <c>instance_id</c>; without one, the first member
    /// that has the body answers.
    /// </summary>
    public static async Task Body(Federator f, HttpContext context)
    {
        var accept = context.Request.Headers.Accept is { Count: > 0 } value ? value.ToString() : null;
        var authorization = ProxyRequest.Authorization(context);

        if (InstanceIdCodec.TryDecode(ProxyRequest.Query(context, "instance_id"), out var memberKey, out var memberInstanceId)
            && f.Registry.Find(memberKey) is { } owner)
        {
            var target = TargetWithInstanceId(f, context, memberInstanceId);
            using var response = await f.Gateway.SendStreaming(owner, Primary, new MemberRequest(HttpMethod.Get, target, authorization, Accept: accept), context.RequestAborted);
            if (response == null)
            {
                await ProxyResponse.Problem(context, StatusCodes.Status502BadGateway, $"Member '{owner.Key}' did not answer.", cancellationToken: context.RequestAborted);
                return;
            }

            await ProxyResponse.RelayStreaming(context, response, context.RequestAborted);
            return;
        }

        var fallbackTarget = TargetWithInstanceId(f, context, null);
        foreach (var member in f.Members(Primary))
        {
            using var response = await f.Gateway.SendStreaming(member, Primary, new MemberRequest(HttpMethod.Get, fallbackTarget, authorization, Accept: accept), context.RequestAborted);
            if (response is { StatusCode: not System.Net.HttpStatusCode.NotFound })
            {
                await ProxyResponse.RelayStreaming(context, response, context.RequestAborted);
                return;
            }
        }

        await ProxyResponse.Problem(context, StatusCodes.Status404NotFound, "No member holds this message body.", cancellationToken: context.RequestAborted);
    }

    /// <summary>
    /// A single retry may name the instance that holds the message; route by it when it does.
    /// </summary>
    public static async Task RetryById(Federator f, HttpContext context)
    {
        if (InstanceIdCodec.TryDecode(ProxyRequest.Query(context, "instance_id"), out var memberKey, out var memberInstanceId)
            && f.Registry.Find(memberKey) is { } owner)
        {
            var request = await ProxyRequest.Repeat(context, f.Prefix(Primary), context.RequestAborted);
            var response = await f.Gateway.Send(owner, Primary, request with { PathAndQuery = TargetWithInstanceId(f, context, memberInstanceId) }, context.RequestAborted);
            await f.RelayOutcome(context, Primary, [response]);
            return;
        }

        await f.RouteToOwners(context, Primary, OwnedRecord.FailedMessage, RouteValue(context, "failedMessageId"));
    }

    static string TargetWithInstanceId(Federator f, HttpContext context, string? memberInstanceId)
    {
        var query = ProxyRequest.QueryWith(context).Where(q => q.Key != "instance_id").ToList();
        if (memberInstanceId != null)
        {
            query.Add(new KeyValuePair<string, string>("instance_id", memberInstanceId));
        }

        return ProxyRequest.WithQuery(context, f.Prefix(Primary), query);
    }

    /// <summary>
    /// A redirect belongs to the broker of the member whose failed messages came from the source
    /// queue. When no member has failures from that queue, every member gets the redirect.
    /// </summary>
    public static async Task CreateRedirect(Federator f, HttpContext context)
    {
        var request = await ProxyRequest.Repeat(context, f.Prefix(Primary), context.RequestAborted);
        var from = request.Body is { Length: > 0 } body ? JsonValues.Text(JsonNode.Parse(body), "fromphysicaladdress") : null;

        var targets = f.Members(Primary);
        if (from != null)
        {
            var maxRows = f.Settings.DeepPagingMaxRows.ToString(CultureInfo.InvariantCulture);
            var queues = await f.FanOut(context, Primary, $"errors/queues/addresses?per_page={maxRows}");
            var owners = queues
                .Where(r => r.IsSuccess && r.Array().Any(q => string.Equals(JsonValues.Text(q, "physical_address"), from, StringComparison.OrdinalIgnoreCase)))
                .Select(r => r.Member)
                .ToList();
            if (owners.Count > 0)
            {
                targets = owners;
            }
        }

        var responses = await f.Gateway.SendAll(targets, Primary, request, context.RequestAborted);
        await f.RelayOutcome(context, Primary, responses);
    }

    public static async Task RetentionStatus(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).Select(r => r.Json()).OfType<JsonObject>().ToList();
        if (successes.Count == 0)
        {
            await f.RelayOutcome(context, Primary, responses);
            return;
        }

        var result = successes[0].DeepClone().AsObject();
        result["is_running"] = successes.Any(s => JsonValues.Bool(s, "is_running") == true);
        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses), context.RequestAborted);
    }

    public static string RouteValue(HttpContext context, string name) => context.Request.RouteValues[name]?.ToString() ?? "";
}
