namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.FileProviders;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            settings.RunCleanupBundle = true;

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

            var servicePulsePath = Path.Combine(AppContext.BaseDirectory, "platform", "servicepulse", "ServicePulse.dll");
            var manifestEmbeddedFileProvider = new ManifestEmbeddedFileProvider(Assembly.LoadFrom(servicePulsePath), "wwwroot");
            var fileProvider = new CompositeFileProvider(hostBuilder.Environment.WebRootFileProvider, manifestEmbeddedFileProvider);
            var defaultFilesOptions = new DefaultFilesOptions { FileProvider = fileProvider };
            var staticFileOptions = new StaticFileOptions { FileProvider = fileProvider };

            var app = hostBuilder.Build();
            app.UseServiceControl()
                .UseMiddleware<AppConstantsMiddleware>()
                .UseDefaultFiles(defaultFilesOptions)
                .UseStaticFiles(staticFileOptions);

            await app.RunAsync(settings.RootUrl);
        }
    }
}
