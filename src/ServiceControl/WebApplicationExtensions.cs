namespace ServiceControl;

using System;
using System.IO;
using System.Reflection;
using Infrastructure.SignalR;
using Infrastructure.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

public static class WebApplicationExtensions
{
    public static IApplicationBuilder UseServiceControl(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });
        app.UseResponseCompression();
        app.UseMiddleware<BodyUrlRouteFix>();
        app.UseHttpLogging();
        app.MapHub<MessageStreamerHub>("/api/messagestream");
        app.UseCors();
        app.MapControllers();

        return app;
    }

    public static IApplicationBuilder UseServicePulse(this WebApplication app)
    {
        var servicePulsePath = Path.Combine(AppContext.BaseDirectory, "platform", "servicepulse", "ServicePulse.dll");
        var manifestEmbeddedFileProvider = new ManifestEmbeddedFileProvider(Assembly.LoadFrom(servicePulsePath), "wwwroot");
        var fileProvider = new CompositeFileProvider(app.Environment.WebRootFileProvider, manifestEmbeddedFileProvider);
        var defaultFilesOptions = new DefaultFilesOptions { FileProvider = fileProvider };
        var staticFileOptions = new StaticFileOptions { FileProvider = fileProvider };

        app.UseMiddleware<AppConstantsMiddleware>()
            .UseDefaultFiles(defaultFilesOptions)
            .UseStaticFiles(staticFileOptions);

        return app;
    }
}