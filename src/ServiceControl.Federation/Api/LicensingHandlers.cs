namespace ServiceControl.Federation.Api;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aggregation;
using Http;
using Members;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Licensing and usage endpoints (<c>api/licensing/*</c>), which describe each installation separately.
/// </summary>
static class LicensingHandlers
{
    const Surface Primary = Surface.Primary;

    /// <summary>
    /// Detected endpoints with their throughput, merged by queue identity. Daily maxima are summed,
    /// which over-estimates when installations peak on different days; members do not expose the
    /// daily series needed to do better.
    /// </summary>
    public static async Task Endpoints(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses))
        {
            return;
        }

        var merged = responses
            .Where(r => r.IsSuccess)
            .SelectMany(r => r.Array().OfType<JsonNode>())
            .GroupBy(row => $"{JsonValues.Text(row, "name_hash")}|{JsonValues.Text(row, "scope_hash")}", StringComparer.Ordinal)
            .Select(group => MergeEndpoint([.. group]));

        await ProxyResponse.WriteJson(context, new JsonArray([.. merged]), f.Meta(Primary, responses), context.RequestAborted);
    }

    static JsonObject MergeEndpoint(IReadOnlyList<JsonNode> rows)
    {
        var result = rows[0].DeepClone().AsObject();
        if (rows.Count == 1)
        {
            return result;
        }

        result["max_daily_throughput"] = rows.Sum(r => (long)(JsonValues.Number(r, "max_daily_throughput") ?? 0));
        result["is_known_endpoint"] = rows.Any(r => JsonValues.Bool(r, "is_known_endpoint") == true);

        if (rows.Any(r => JsonValues.At(r, "average_monthly_throughput") != null))
        {
            result["average_monthly_throughput"] = rows.Sum(r => (long)(JsonValues.Number(r, "average_monthly_throughput") ?? 0));
        }

        if (rows.Any(r => JsonValues.At(r, "monthly_throughput") is JsonArray))
        {
            result["monthly_throughput"] = new JsonArray([.. rows
                .SelectMany(r => JsonValues.At(r, "monthly_throughput") as JsonArray ?? [])
                .OfType<JsonNode>()
                .GroupBy(m => JsonValues.Text(m, "month") ?? "", StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new JsonObject
                {
                    ["month"] = g.Key,
                    ["throughput"] = g.Sum(m => (long)(JsonValues.Number(m, "throughput") ?? 0))
                })]);
        }

        return result;
    }

    /// <summary>
    /// A report can be generated only when every installation can generate its own.
    /// </summary>
    public static async Task ReportAvailable(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).ToList();
        var result = successes[0].Json()!.DeepClone().AsObject();
        var blocked = successes.FirstOrDefault(r => JsonValues.Bool(r.Json(), "report_can_be_generated") == false);
        var unreachable = responses.FirstOrDefault(r => !r.IsSuccess);

        if (blocked != null)
        {
            result["report_can_be_generated"] = false;
            result["reason"] = $"{blocked.Member.Name}: {JsonValues.Text(blocked.Json(), "reason")}";
        }
        else if (unreachable != null)
        {
            result["report_can_be_generated"] = false;
            result["reason"] = $"{unreachable.Member.Name} did not answer.";
        }

        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Connection tests pass only when they pass on every installation; error messages name the member.
    /// </summary>
    public static async Task SettingsTest(Federator f, HttpContext context)
    {
        var responses = await f.FanOut(context, Primary);
        if (await f.NothingAnswered(context, Primary, responses, notFoundIsAnAnswer: false))
        {
            return;
        }

        var successes = responses.Where(r => r.IsSuccess).ToList();
        var result = successes[0].Json()!.DeepClone().AsObject();

        foreach (var name in result.Select(p => p.Key).Where(k => k.EndsWith("_connection_result", StringComparison.Ordinal)).ToList())
        {
            var merged = result[name] as JsonObject ?? [];
            merged["connection_successful"] = successes.All(r => JsonValues.Bool(r.Json(), $"{name}.connection_successful") != false)
                && responses.All(r => r.IsSuccess);
            merged["connection_error_messages"] = new JsonArray([.. successes
                .SelectMany(r => (JsonValues.At(r.Json(), $"{name}.connection_error_messages") as JsonArray ?? [])
                    .Select(m => (JsonNode)$"{r.Member.Name}: {JsonValues.AsText(m)}"))
                .Concat(responses.Where(r => !r.IsSuccess).Select(r => (JsonNode)$"{r.Member.Name} did not answer."))]);
            result[name] = merged;
        }

        await ProxyResponse.WriteJson(context, result, f.Meta(Primary, responses), context.RequestAborted);
    }

    /// <summary>
    /// Usage reports are signed per installation and cannot be merged. With one member the report is
    /// relayed; with several, the signed reports are bundled into one zip, one entry per member.
    /// </summary>
    public static async Task ReportFile(Federator f, HttpContext context)
    {
        var members = f.Members(Primary);
        var target = f.Target(context, Primary);
        var request = new MemberRequest(HttpMethod.Get, target, ProxyRequest.Authorization(context), Accept: "*/*");

        if (members.Count == 1)
        {
            using var single = await f.Gateway.SendStreaming(members[0], Primary, request, context.RequestAborted);
            if (single == null)
            {
                await ProxyResponse.Problem(context, StatusCodes.Status502BadGateway, "The member did not answer.", cancellationToken: context.RequestAborted);
                return;
            }

            await ProxyResponse.RelayStreaming(context, single, context.RequestAborted);
            return;
        }

        var reports = new List<(Member Member, string FileName, byte[] Content)>();
        var failures = new List<(Member Member, string Reason)>();
        foreach (var member in members)
        {
            using var response = await f.Gateway.SendStreaming(member, Primary, request, context.RequestAborted);
            if (response is not { IsSuccessStatusCode: true })
            {
                var reason = response == null ? "did not answer" : await response.Content.ReadAsStringAsync(context.RequestAborted);
                failures.Add((member, reason));
                continue;
            }

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "throughput-report.zip";
            reports.Add((member, fileName, await response.Content.ReadAsByteArrayAsync(context.RequestAborted)));
        }

        if (reports.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(string.Join(Environment.NewLine, failures.Select(failure => $"{failure.Member.Name}: {failure.Reason}")), context.RequestAborted);
            return;
        }

        using var bundle = new MemoryStream();
        using (var archive = new ZipArchive(bundle, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (member, fileName, content) in reports)
            {
                var entry = archive.CreateEntry($"{member.Key}-{fileName}", CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/zip";
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"federated.throughput-reports-{stamp}.zip\"";
        if (failures.Count > 0)
        {
            context.Response.Headers[ProxyResponse.IncompleteResultsHeader] = string.Join(", ", failures.Select(failure => $"{failure.Member.PrimaryUri.ToString().TrimEnd('/')}:error"));
        }

        bundle.Position = 0;
        await bundle.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}
