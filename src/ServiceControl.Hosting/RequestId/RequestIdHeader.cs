#nullable enable
namespace ServiceControl.Hosting.RequestId;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Surfaces the per-request id (the same value used as the audit operation id) on every response so
/// callers can correlate and quote it. <see cref="HttpContext.TraceIdentifier"/> is stable for the
/// request; OnStarting applies it just before the response flushes.
/// </summary>
public static class RequestIdHeader
{
    public const string HeaderName = "Request-Id";

    public static void UseRequestIdHeader(this WebApplication app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                Apply((HttpContext)state);
                return Task.CompletedTask;
            }, context);

            return next(context);
        });

    // Set-if-absent: a response proxied from a remote instance already carries the remote's
    // Request-Id — the id its audit entries are correlated by — and that is the id the caller
    // must receive.
    public static void Apply(HttpContext httpContext) =>
        httpContext.Response.Headers.TryAdd(HeaderName, httpContext.TraceIdentifier);
}
