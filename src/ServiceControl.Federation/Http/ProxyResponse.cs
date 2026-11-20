namespace ServiceControl.Federation.Http;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Members;
using Microsoft.AspNetCore.Http;

/// <summary>
/// What the proxy adds to an aggregated response besides the body.
/// </summary>
public sealed class ResponseMeta
{
    public int Status { get; init; } = StatusCodes.Status200OK;

    public long? TotalCount { get; init; }

    public string? ETag { get; init; }

    public string? Link { get; init; }

    public string? Version { get; init; }

    public IReadOnlyList<string> Incomplete { get; init; } = [];
}

public static class ProxyResponse
{
    public const string IncompleteResultsHeader = "X-Particular-Incomplete-Results";

    /// <summary>
    /// <c>X-Particular-Incomplete-Results</c> entries: one per member that contributed nothing, named by
    /// its API URL, plus the entries members reported for their own audit instances.
    /// </summary>
    public static List<string> IncompleteEntries(IEnumerable<MemberResponse> responses, Surface surface)
    {
        var entries = new List<string>();
        foreach (var response in responses)
        {
            var url = response.Member.BaseUri(surface)?.ToString().TrimEnd('/') ?? response.Member.Key;
            if (response.Failure is { } reason)
            {
                entries.Add($"{url}:{ReasonName(reason)}");
            }
            else if (!response.IsSuccess && !response.IsNotFound)
            {
                // Answered, but with an error, so it contributed nothing.
                entries.Add($"{url}:{ReasonName(FailureReason.Error)}");
            }
            else if (response.Header(IncompleteResultsHeader) is { Length: > 0 } memberEntries)
            {
                entries.AddRange(memberEntries.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return entries;
    }

    public static string ReasonName(FailureReason reason) => reason switch
    {
        FailureReason.Timeout => "timeout",
        FailureReason.Unavailable => "unavailable",
        FailureReason.Error => "error",
        _ => "error"
    };

    public static async Task WriteJson(HttpContext context, JsonNode? body, ResponseMeta meta, CancellationToken cancellationToken = default)
    {
        WriteHeaders(context, meta);

        if (meta.ETag != null && EntityTags.Matches(context.Request.Headers.IfNoneMatch, meta.ETag))
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        context.Response.StatusCode = meta.Status;
        if (HttpMethods.IsHead(context.Request.Method) || body == null)
        {
            return;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        await using var writer = new Utf8JsonWriter(context.Response.Body);
        body.WriteTo(writer);
        await writer.FlushAsync(cancellationToken);
    }

    public static void WriteHeaders(HttpContext context, ResponseMeta meta)
    {
        var headers = context.Response.Headers;
        if (meta.TotalCount is { } total)
        {
            headers["Total-Count"] = total.ToString(CultureInfo.InvariantCulture);
        }

        if (meta.ETag != null)
        {
            headers.ETag = meta.ETag;
        }

        if (meta.Link != null)
        {
            headers.Link = meta.Link;
        }

        if (meta.Version != null)
        {
            headers["X-Particular-Version"] = meta.Version;
        }

        if (meta.Incomplete.Count > 0)
        {
            headers[IncompleteResultsHeader] = string.Join(", ", meta.Incomplete);
        }

        headers.CacheControl = "private, max-age=0";
        headers.Vary = "Accept";
    }

    /// <summary>
    /// Relays a buffered member response unchanged, apart from headers that describe the hop.
    /// </summary>
    public static async Task Relay(HttpContext context, MemberResponse response, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = response.Status;
        foreach (var (name, value) in response.Headers)
        {
            if (!IsHopHeader(name))
            {
                context.Response.Headers[name] = value;
            }
        }

        if (response.Body.Length > 0 && !HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.Body.WriteAsync(response.Body, cancellationToken);
        }
    }

    /// <summary>
    /// Streams a member response to the caller.
    /// </summary>
    public static async Task RelayStreaming(HttpContext context, HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (!IsHopHeader(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await stream.CopyToAsync(context.Response.Body, cancellationToken);
        }
    }

    public static Task Problem(HttpContext context, int status, string title, IReadOnlyList<string>? incomplete = null, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["type"] = "https://tools.ietf.org/html/rfc9110#section-15",
            ["title"] = title,
            ["status"] = status
        };

        WriteHeaders(context, new ResponseMeta { Incomplete = incomplete ?? [] });
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(body.ToJsonString(), cancellationToken);
    }

    /// <summary>
    /// Paging links in the shape ServiceControl emits: relative to the API root, with the page replaced.
    /// </summary>
    public static string? PagingLinks(HttpContext context, string surfacePrefix, int page, int pageSize, long total)
    {
        if (total <= pageSize)
        {
            return null;
        }

        var lastPage = (int)Math.Ceiling(total / (double)pageSize);
        var links = new List<string>();

        void Add(int target, string rel)
        {
            var query = ProxyRequest.QueryWith(context, ("page", target.ToString(CultureInfo.InvariantCulture)), ("per_page", pageSize.ToString(CultureInfo.InvariantCulture)));
            links.Add($"<{ProxyRequest.WithQuery(context, surfacePrefix, query)}>; rel=\"{rel}\"");
        }

        if (page < lastPage)
        {
            Add(page + 1, "next");
        }

        if (page > 1)
        {
            Add(page - 1, "prev");
        }

        Add(1, "first");
        Add(lastPage, "last");
        return string.Join(", ", links);
    }

    static bool IsHopHeader(string name) =>
        HopHeaders.Contains(name) || name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase);

    static readonly HashSet<string> HopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection", "Content-Length",
        "Content-Encoding", "Server", "Date", "Vary"
    };
}
