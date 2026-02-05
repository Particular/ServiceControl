namespace ServiceControl.Hosting.Https;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlHttps(this WebApplication app, HttpsSettings settings)
    {
        // EnableHsts is disabled by default
        // Hsts is automatically disabled in Development environments
        if (settings.EnableHsts && !app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // RedirectHttpToHttps is disabled by default.
        if (settings.RedirectHttpToHttps)
        {
            app.UseHttpsRedirection();
        }
    }
}
