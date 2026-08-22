namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Linq;
    using System.Net;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Headers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.Infrastructure;

    class NotModifiedStatusHttpHandler : IResultFilter
    {
        static bool IfNoneMatch(RequestHeaders requestHeaders, ResponseHeaders responseHeaders)
        {
            var current = responseHeaders.ETag;

            if (current is null)
            {
                return false;
            }

            // EntityTagHeaderValue.Equals compares strength as well as tag, and its own documentation
            // says not to use it for this. RFC 9110 requires If-None-Match to use weak comparison.
            return requestHeaders.IfNoneMatch.Any(candidate =>
                candidate.Tag.Equals("*", StringComparison.Ordinal) || candidate.Compare(current, useStrongComparison: false));
        }

        static bool IfNotModifiedSince(DateTimeOffset? ifModifiedSince, DateTimeOffset? lastModified) =>
            lastModified <= ifModifiedSince;

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.HttpContext.Response.HasStarted)
            {
                // In forwarding scenarios we don't want to alter headers set by other instances
                return;
            }

            if (!IsSuccess(context))
            {
                return;
            }

            var requestHeaders = context.HttpContext.Request.GetTypedHeaders();
            var responseHeaders = context.HttpContext.Response.GetTypedHeaders();

            var ifNoneMatch = IfNoneMatch(requestHeaders, responseHeaders);
            var ifNotModifiedSince = IfNotModifiedSince(requestHeaders.IfModifiedSince, responseHeaders.LastModified);

            if (ifNoneMatch || ifNotModifiedSince)
            {
                // The replaced result never executes, so whatever it owned would never be released.
                if (context.Result is FileStreamResult file)
                {
                    context.HttpContext.Response.RegisterForDisposeAsync(file.FileStream);
                }

                context.Result = new StatusCodeResult((int)HttpStatusCode.NotModified);
            }
        }

        // Response.StatusCode is still whatever the pipeline defaulted to, because the result that
        // would set it has not executed yet.
        static bool IsSuccess(ResultExecutingContext context) =>
            context.Result is IStatusCodeActionResult { StatusCode: { } statusCode }
                ? statusCode is >= 200 and <= 299
                : context.HttpContext.Response.StatusCode is >= 200 and <= 299;

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}