#nullable enable
namespace ServiceControl.Hosting.QueryTimeout;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure;

/// <summary>
/// Answers a request whose database query ran out of its allowed query time with 504 Gateway Timeout and a
/// problem body that names the setting, so a caller can tell a timeout from a crash and from an empty result.
/// The data stores raise <see cref="TimeoutException"/> for it, see <see cref="QueryTimeLimit"/>.
/// </summary>
public static class QueryTimeoutResponse
{
    static readonly ILogger logger = LoggerUtil.CreateStaticLogger(typeof(QueryTimeoutResponse));

    public static void UseQueryTimeoutResponse(this WebApplication app) => app.Use(Wrap);

    public static RequestDelegate Wrap(RequestDelegate next) => async context =>
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (TimeoutException e) when (!context.Response.HasStarted)
        {
            logger.LogWarning(e, "The query behind {Method} {Path} did not complete within its allowed query time", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            // Deliberately no "type" URI (RFC 9457 then treats it as about:blank): the body stays purely
            // descriptive rather than linking to documentation, which only search or permalink URLs may do.
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "The query did not complete within the allowed query time",
                Detail = e.Message
            }, options: null, contentType: "application/problem+json", context.RequestAborted).ConfigureAwait(false);
        }
    };
}
