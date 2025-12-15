namespace ServiceControl.Hosting.Https;

using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Infrastructure;

public static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlHttps(this WebApplicationBuilder hostBuilder, HttpsSettings settings)
    {
        if (settings.EnableHsts)
        {
            hostBuilder.Services.Configure<HstsOptions>(options =>
            {
                options.MaxAge = TimeSpan.FromSeconds(settings.HstsMaxAgeSeconds);
                options.IncludeSubDomains = settings.HstsIncludeSubDomains;
            });
        }

        if (settings.RedirectHttpToHttps && settings.HttpsPort.HasValue)
        {
            hostBuilder.Services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = settings.HttpsPort.Value;
            });
        }

        if (settings.Enabled)
        {
            hostBuilder.WebHost.ConfigureKestrel(kestrel =>
            {
                kestrel.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = LoadCertificate(settings);
                });
            });
        }
    }

    static X509Certificate2 LoadCertificate(HttpsSettings settings)
    {
        if (string.IsNullOrEmpty(settings.CertificatePassword))
        {
            return new X509Certificate2(settings.CertificatePath);
        }

        return new X509Certificate2(settings.CertificatePath, settings.CertificatePassword);
    }
}
