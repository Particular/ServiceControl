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
        static bool IfNoneMatch(RequestHeaders requestHeaders, ResponseHeaders responseHeaders)
        {
            return responseHeaders.ETag != null && requestHeaders.IfNoneMatch.Contains(responseHeaders.ETag);
        }

        static bool IfNotModifiedSince(DateTimeOffset? ifModifiedSince, DateTimeOffset? lastModified)
        {
            if (lastModified == null)
            {
                return false;
            }

            return lastModified <= ifModifiedSince;
        }

//         // currently lastModified is not supported without returning a content which would violate the HTTP spec
//         // it can be resurrected once ASP.NET Core is in place.
// #pragma warning disable IDE0060 // Remove unused parameter
//         static HttpResponseMessage Get304ResponseMessage(HttpResponseHeaders responseHeaders,
//             DateTimeOffset? lastModified, HttpRequestMessage request)
// #pragma warning restore IDE0060 // Remove unused parameter
//         {
//             var response = request.CreateResponse(HttpStatusCode.NotModified);
//
//             if (responseHeaders.ETag.Tag != null)
//             {
//                 response.Headers.ETag = responseHeaders.ETag;
//             }
//
//             return response;
//         }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            // TODO how do we this?
            // if (!context.HttpContext IsSuccessStatusCode)
            // {
            //     return response;
            // }

            var requestHeaders = context.HttpContext.Request.GetTypedHeaders();
            var responseHeaders = context.HttpContext.Response.GetTypedHeaders();

            var ifNoneMatch = IfNoneMatch(requestHeaders, responseHeaders);
            var ifNotModifiedSince = IfNotModifiedSince(requestHeaders.IfModifiedSince, responseHeaders?.LastModified);

            if (ifNoneMatch || ifNotModifiedSince)
            {
                // TODO previously we wewre creating a brand new response, now we're adding more headers than before
                context.Result = new StatusCodeResult((int)HttpStatusCode.NotModified);
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}