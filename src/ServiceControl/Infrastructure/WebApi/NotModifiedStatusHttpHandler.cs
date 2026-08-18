namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Linq;
    using System.Net;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Headers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

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

            var statusCode = context.HttpContext.Response.StatusCode;
            if (statusCode is < 200 or > 299)
            {
                return;
            }

            var requestHeaders = context.HttpContext.Request.GetTypedHeaders();
            var responseHeaders = context.HttpContext.Response.GetTypedHeaders();

            var ifNoneMatch = IfNoneMatch(requestHeaders, responseHeaders);
            var ifNotModifiedSince = IfNotModifiedSince(requestHeaders.IfModifiedSince, responseHeaders.LastModified);

            if (ifNoneMatch || ifNotModifiedSince)
            {
                context.Result = new StatusCodeResult((int)HttpStatusCode.NotModified);
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}