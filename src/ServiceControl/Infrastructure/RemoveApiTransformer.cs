namespace ServiceControl.Infrastructure
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Yarp.ReverseProxy.Forwarder;

    sealed class RemoveApiTransformer : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(HttpContext httpContext,
            HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
        {
            // Copy all request headers
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);
            proxyRequest.RequestUri = RequestUtilities.MakeDestinationAddress(
                destinationPrefix,
                httpContext.Request.Path.ToString().Replace("/api", string.Empty),
                httpContext.Request.QueryString);
        }

        public static HttpTransformer Instance { get; } = new RemoveApiTransformer();
    }
}