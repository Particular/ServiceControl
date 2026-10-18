namespace ServiceControl.Federation.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aggregation;
using Http;
using Members;
using Microsoft.AspNetCore.Http;

/// <summary>
/// The Monitoring surface. Members without a Monitoring URL do not take part.
/// </summary>
static class MonitoringHandlers
{
    const Surface Monitoring = Surface.Monitoring;

    static readonly HashSet<int> HistoryPeriods = [1, 5, 10, 15, 30, 60];

    public static async Task Root(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Monitoring, "");
        if (await f.NothingAnswered(context, Monitoring, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var body = responses.First(r => r.IsSuccess).Json()?.DeepClone() as JsonObject ?? [];
        var version = f.LowestVersion(Monitoring);
        if (version != null && body.ContainsKey("version"))
        {
            body["version"] = version;
        }

        PrimaryHandlers.MergePlatformHealth(body, responses);
        await ProxyResponse.WriteJson(context, body, new ResponseMeta { Version = version }, context.RequestAborted);
    }

    /// <summary>
    /// ServicePulse reads <c>Allow</c> to decide whether instances can be removed, so only operations
    /// every member supports are advertised.
    /// </summary>
    public static async Task Options(Federator f, HttpContext context)
    {
        var responses = await f.Gateway.SendAll(f.Members(Monitoring), Monitoring, new MemberRequest(HttpMethod.Options, "", ProxyRequest.Authorization(context)), context.RequestAborted);
        var allowed = responses
            .Where(r => r.IsSuccess)
            .Select(r => (r.Header("Allow") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase))
            .DefaultIfEmpty([])
            .Aggregate((a, b) =>
            {
                a.IntersectWith(b);
                return a;
            });

        context.Response.Headers.Allow = string.Join(", ", allowed);
        context.Response.Headers.AccessControlExposeHeaders = "Allow";
        var version = f.LowestVersion(Monitoring);
        if (version != null)
        {
            context.Response.Headers["X-Particular-Version"] = version;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    public static async Task Endpoints(Federator f, HttpContext context)
    {
        if (!await ValidHistory(context))
        {
            return;
        }

        var responses = await f.FanOut(context, Monitoring);
        if (await f.NothingAnswered(context, Monitoring, responses))
        {
            return;
        }

        var merged = MonitoredEndpointMerger.MergeList(Lists(responses));
        await ProxyResponse.WriteJson(context, merged, f.Meta(Monitoring, responses), context.RequestAborted);
    }

    public static async Task EndpointDetails(Federator f, HttpContext context)
    {
        if (!await ValidHistory(context))
        {
            return;
        }

        var responses = await f.FanOut(context, Monitoring);
        if (await f.NothingAnswered(context, Monitoring, responses))
        {
            return;
        }

        var details = responses.Where(r => r.IsSuccess).Select(r => r.Json()).OfType<JsonNode>().ToList();
        var merged = MonitoredEndpointMerger.MergeDetails(details);
        await ProxyResponse.WriteJson(context, merged, f.Meta(Monitoring, responses), context.RequestAborted);
    }

    /// <summary>
    /// Recomputed from the merged list: an endpoint is disconnected when every instance on every member
    /// is stale. Summing the members' counts would be wrong once endpoints are merged by name.
    /// </summary>
    public static async Task Disconnected(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Monitoring, "monitored-endpoints?history=1");
        if (await f.NothingAnswered(context, Monitoring, responses))
        {
            return;
        }

        var merged = MonitoredEndpointMerger.MergeList(Lists(responses));
        var disconnected = merged.Count(endpoint => JsonValues.Bool(endpoint, "isStale") == true);
        await ProxyResponse.WriteJson(context, JsonValue.Create(disconnected), f.Meta(Monitoring, responses), context.RequestAborted);
    }

    public static Task MyRoutes(Federator f, HttpContext context) => PrimaryHandlers.MyRoutes(f, context, Monitoring, "api/my/routes");

    public static Task License(Federator f, HttpContext context) => PrimaryHandlers.License(f, context, Monitoring);

    static List<IReadOnlyList<JsonNode>> Lists(IEnumerable<MemberResponse> responses) =>
        [.. responses.Select(r => (IReadOnlyList<JsonNode>)[.. r.IsSuccess ? r.Array().OfType<JsonNode>() : []])];

    /// <summary>
    /// Monitoring throws on unknown history periods; answer 400 instead of fanning out a 500.
    /// </summary>
    static async Task<bool> ValidHistory(HttpContext context)
    {
        var history = ProxyRequest.Query(context, "history");
        if (history == null || (int.TryParse(history, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minutes) && HistoryPeriods.Contains(minutes)))
        {
            return true;
        }

        await ProxyResponse.Problem(context, StatusCodes.Status400BadRequest, $"Unknown history period '{history}'. Use one of {string.Join(", ", HistoryPeriods)}.", cancellationToken: context.RequestAborted);
        return false;
    }
}
