namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Net;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Headers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    class NotModifiedStatusHttpHandler : IResultFilter
    {
        static bool IfNoneMatch(RequestHeaders requestHeaders, ResponseHeaders responseHeaders) =>
            responseHeaders.ETag != null && requestHeaders.IfNoneMatch.Contains(responseHeaders.ETag);

        static bool IfNotModifiedSince(DateTimeOffset? ifModifiedSince, DateTimeOffset? lastModified) =>
            lastModified <= ifModifiedSince;

        public void OnResultExecuting(ResultExecutingContext context)
        {
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
                // TODO previously we were creating a brand new response, now we're adding more headers than before
                context.Result = new StatusCodeResult((int)HttpStatusCode.NotModified);
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}