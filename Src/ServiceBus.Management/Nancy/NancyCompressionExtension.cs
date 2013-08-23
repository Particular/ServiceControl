namespace ServiceBus.Management.Nancy
{
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.Linq;
    using global::Nancy;

    public static class NancyCompressionExtension
    {
        public static void CheckForCompression(NancyContext context)
        {
            if (!RequestIsGzipCompatible(context.Request))
            {
                return;
            }

            if (context.Response.StatusCode != HttpStatusCode.OK)
            {
                return;
            }

            if (!ResponseIsCompatibleMimeType(context.Response))
            {
                return;
            }

            if (ContentLengthIsTooSmall(context.Response))
            {
                return;
            }

            CompressResponse(context.Response);
        }

        static void CompressResponse(Response response)
        {
            response.Headers["Content-Encoding"] = "gzip";

            var contents = response.Contents;
            response.Contents = responseStream =>
            {
                using (var compression = new GZipStream(responseStream, CompressionMode.Compress))
                {
                    contents(compression);
                }
            };
        }

        static bool ContentLengthIsTooSmall(Response response)
        {
            string contentLength;
            if (response.Headers.TryGetValue("Content-Length", out contentLength))
            {
                var length = long.Parse(contentLength);
                if (length < 4096)
                {
                    return true;
                }
            }
            return false;
        }

        static bool ResponseIsCompatibleMimeType(Response response)
        {
            return ValidMimes.Any(x => x == response.ContentType);
        }

        static bool RequestIsGzipCompatible(Request request)
        {
            return request.Headers.AcceptEncoding.Any(x => x.Contains("gzip"));
        }

        static readonly List<string> ValidMimes = new List<string>
        {
            "text/css",
            "text/html",
            "text/plain",
            "application/xml",
            "text/xml",
            "application/json",
            "text/json",
            "application/xaml+xml",
            "application/x-javascript"
        };
    }
}