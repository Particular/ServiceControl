namespace ServiceBus.Management.Api.Nancy
{
    using System;
    using System.Globalization;
    using System.Linq;
    using global::Nancy;

    public static class NotModifiedStatusExtension
    {
        public static void Check(NancyContext ctx)
        {
            var requestHeaders = ctx.Request.Headers;
            var responseHeaders = ctx.Response.Headers;
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
                ctx.Response = new Response { StatusCode = HttpStatusCode.NotModified };
                
                if (currentEtag != null)
                {
                    ctx.Response
                       .WithHeader("ETag", currentEtag);
                }
                if (currentLastModified != null)
                {
                    ctx.Response.
                        WithHeader("Last-Modified", currentLastModified);
                }
            }
        }
    }
}