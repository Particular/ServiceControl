namespace ServiceControl.AcceptanceTesting.ForwardedHeaders
{
    using System.Net;
    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// Test middleware that sets RemoteIpAddress from X-Test-Remote-IP header.
    /// This enables testing ForwardedHeaders middleware's KnownProxies/KnownNetworks behavior.
    /// </summary>
    public static class TestRemoteIpMiddleware
    {
        /// <summary>
        /// The header name used to specify a test remote IP address.
        /// </summary>
        public const string HeaderName = "X-Test-Remote-IP";

        /// <summary>
        /// Adds middleware that sets the connection's RemoteIpAddress from the X-Test-Remote-IP header.
        /// This must be called BEFORE UseServiceControl/UseServiceControlAudit/UseServiceControlMonitoring
        /// so that the ForwardedHeaders middleware can properly check KnownProxies/KnownNetworks.
        /// </summary>
        public static void UseTestRemoteIp(this WebApplication app) => app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var testIpHeader))
            {
                var testIpValue = testIpHeader.ToString();
                if (IPAddress.TryParse(testIpValue, out var testIp))
                {
                    context.Connection.RemoteIpAddress = testIp;
                }
            }
            await next();
        });
    }
}
