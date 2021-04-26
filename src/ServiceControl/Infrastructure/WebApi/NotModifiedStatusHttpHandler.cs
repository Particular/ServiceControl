namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    class NotModifiedStatusHttpHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return response;
            }

            var requestHeaders = request.Headers;
            var responseHeaders = response.Headers;

            var ifNoneMatch = IfNoneMatch(requestHeaders, responseHeaders);
            var ifNotModifiedSince = IfNotModifiedSince(requestHeaders.IfModifiedSince, response.Content?.Headers?.LastModified);

            if (ifNoneMatch || ifNotModifiedSince)
            {
                return Get304ResponseMessage(responseHeaders, response.Content?.Headers?.LastModified, request);
            }

            return response;
        }

        static bool IfNoneMatch(HttpRequestHeaders requestHeaders, HttpResponseHeaders responseHeaders)
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

        // currently lastModified is not supported without returning a content which would violate the HTTP spec
        // it can be resurrected once ASP.NET Core is in place.
#pragma warning disable IDE0060 // Remove unused parameter
        static HttpResponseMessage Get304ResponseMessage(HttpResponseHeaders responseHeaders, DateTimeOffset? lastModified, HttpRequestMessage request)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var response = request.CreateResponse(HttpStatusCode.NotModified);

            if (responseHeaders.ETag.Tag != null)
            {
                response.Headers.ETag = responseHeaders.ETag;
            }

            return response;
        }
    }
}