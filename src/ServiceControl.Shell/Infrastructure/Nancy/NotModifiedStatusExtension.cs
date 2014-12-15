namespace ServiceBus.Management.Infrastructure.Nancy
{
    using System;
    using System.Globalization;
    using System.Linq;
    using global::Nancy;

    public static class NotModifiedStatusExtension
    {
        public static void Check(NancyContext context)
        {
            var requestHeaders = context.Request.Headers;
            var responseHeaders = context.Response.Headers;
            string currentEtag = null;
            string currentLastModified = null;
            var send304 = false;

            if (responseHeaders.ContainsKey("ETag"))
            {
                currentEtag = responseHeaders["ETag"];

                if (requestHeaders.IfNoneMatch.Contains(currentEtag))
                {
                    send304 = true;
                }
            }

            if (responseHeaders.ContainsKey("Last-Modified"))
            {
                currentLastModified = responseHeaders["Last-Modified"];

                var responseLastModified = DateTime.ParseExact(currentLastModified, "R",
                    CultureInfo.InvariantCulture, DateTimeStyles.None);
                if (responseLastModified <= requestHeaders.IfModifiedSince)
                {
                    send304 = true;
                }
            }

            if (send304)
            {
                context.Response = new Response {StatusCode = HttpStatusCode.NotModified};

                if (currentEtag != null)
                {
                    context.Response
                        .WithHeader("ETag", currentEtag);
                }
                if (currentLastModified != null)
                {
                    context.Response.
                        WithHeader("Last-Modified", currentLastModified);
                }
            }
        }
    }
}