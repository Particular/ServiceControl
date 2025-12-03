namespace ServiceControl.Hosting.Https;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using ServiceControl.Infrastructure;

public static class WebApplicationExtensions
{
    public static void UseServiceControlHttps(this WebApplication app, HttpsSettings settings)
    {
        if (settings.EnableHsts && !app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        if (settings.RedirectHttpToHttps)
        {
            app.UseHttpsRedirection();
        }
    }
}
