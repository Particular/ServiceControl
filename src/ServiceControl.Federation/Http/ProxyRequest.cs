namespace ServiceControl.Federation.Http;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Members;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Reads what the proxy needs from an incoming request: the member-relative path, the caller's
/// credentials and body, and the paging parameters.
/// </summary>
public static class ProxyRequest
{
    /// <summary>
    /// The request target relative to the surface prefix, exactly as the client encoded it, for
    /// example <c>recoverability/groups/Endpoint%20Name?classifierFilter=Sales</c>.
    /// </summary>
    public static string RelativeTarget(HttpContext context, string surfacePrefix)
    {
        var raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        var prefix = context.Request.PathBase.Add(new PathString(surfacePrefix)).ToUriComponent();

        string relative;
        if (!string.IsNullOrEmpty(raw) && raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            relative = raw[prefix.Length..];
        }
        else
        {
            var path = context.Request.Path.ToUriComponent();
            relative = (path.StartsWith(surfacePrefix, StringComparison.OrdinalIgnoreCase) ? path[surfacePrefix.Length..] : path)
                + context.Request.QueryString.ToUriComponent();
        }

        // Clients sometimes build "api//messages/..." (see BodyUrlRouteFix in ServiceControl).
        return relative.TrimStart('/');
    }

    public static string RelativePath(HttpContext context, string surfacePrefix)
    {
        var target = RelativeTarget(context, surfacePrefix);
        var queryStart = target.IndexOf('?');
        return queryStart < 0 ? target : target[..queryStart];
    }

    /// <summary>
    /// The same relative path with a replaced query string.
    /// </summary>
    public static string WithQuery(HttpContext context, string surfacePrefix, IEnumerable<KeyValuePair<string, string>> query)
    {
        var builder = new QueryBuilder(query);
        return RelativePath(context, surfacePrefix) + builder.ToQueryString().ToUriComponent();
    }

    /// <summary>
    /// The incoming query with some parameters replaced or added.
    /// </summary>
    public static List<KeyValuePair<string, string>> QueryWith(HttpContext context, params (string Name, string Value)[] overrides)
    {
        var names = overrides.Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var query = context.Request.Query
            .Where(q => !names.Contains(q.Key))
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string>(q.Key, v ?? "")))
            .ToList();
        query.AddRange(overrides.Select(o => new KeyValuePair<string, string>(o.Name, o.Value)));
        return query;
    }

    public static string? Authorization(HttpContext context) =>
        context.Request.Headers.Authorization is { Count: > 0 } value ? value.ToString() : null;

    public static async Task<byte[]?> ReadBody(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context.Request.ContentLength == 0 || (context.Request.ContentLength == null && !context.Request.Headers.ContainsKey("Transfer-Encoding")))
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>
    /// A member request that repeats the incoming one: same method, target, credentials and body.
    /// </summary>
    public static async Task<MemberRequest> Repeat(HttpContext context, string surfacePrefix, CancellationToken cancellationToken = default) =>
        new(
            new HttpMethod(context.Request.Method),
            RelativeTarget(context, surfacePrefix),
            Authorization(context),
            await ReadBody(context, cancellationToken),
            context.Request.ContentType,
            context.Request.Headers.Accept is { Count: > 0 } accept ? accept.ToString() : null);

    public static MemberRequest Get(HttpContext context, string pathAndQuery) =>
        new(HttpMethod.Get, pathAndQuery, Authorization(context));

    /// <summary>
    /// The proxy's own base URL for a surface, as the client sees it (forwarded headers applied).
    /// </summary>
    public static string BaseUrl(HttpContext context, string surfacePrefix)
    {
        var request = context.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}{surfacePrefix.TrimEnd('/')}/";
    }

    public static int IntQuery(HttpContext context, string name, int defaultValue)
    {
        var value = context.Request.Query[name];
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    public static string? Query(HttpContext context, string name) =>
        context.Request.Query.TryGetValue(name, out var value) && !StringValues.IsNullOrEmpty(value) ? value.ToString() : null;
}
