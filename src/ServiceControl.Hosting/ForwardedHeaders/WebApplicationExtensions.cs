namespace ServiceControl.Hosting.ForwardedHeaders;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlForwardedHeaders(this WebApplication app, ForwardedHeadersSettings settings)
    {
        if (!settings.Enabled)
        {
            return;
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All
        };

        if (!settings.TrustAllProxies)
        {
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            foreach (var proxy in settings.KnownProxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in settings.KnownNetworks)
            {
                options.KnownNetworks.Add(IPNetwork.Parse(network));
            }
        }

        app.UseForwardedHeaders(options);
    }
}
