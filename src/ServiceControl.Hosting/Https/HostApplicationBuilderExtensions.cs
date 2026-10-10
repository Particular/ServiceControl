namespace ServiceControl.Hosting.Https;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Infrastructure;

public static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlHttps(this WebApplicationBuilder hostBuilder, HttpsSettings settings)
    {
        // EnableHsts is disabled by default
        // Hsts is automatically disabled in Development environments
        if (settings.EnableHsts)
        {
            hostBuilder.Services.Configure<HstsOptions>(options =>
            {
                options.MaxAge = TimeSpan.FromSeconds(settings.HstsMaxAgeSeconds);
                options.IncludeSubDomains = settings.HstsIncludeSubDomains;
            });
        }

        // RedirectHttpToHttps is disabled by default. HttpsPort is null by default.
        if (settings.RedirectHttpToHttps && settings.HttpsPort.HasValue)
        {
            hostBuilder.Services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = settings.HttpsPort.Value;
            });
        }

        // Kestrel HTTPS is disabled by default
        if (settings.Enabled)
        {
            // The certificate was loaded and validated when HttpsSettings was constructed. Doing it
            // here instead would defer the failure to endpoint binding, which happens after every
            // hosted service has already started and has to be torn down again.
            var certificate = settings.Certificate ?? throw new InvalidOperationException("HTTPS is enabled but no certificate was loaded.");

            hostBuilder.WebHost.ConfigureKestrel(kestrel =>
            {
                kestrel.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = certificate;
                });
            });
        }
    }
}
