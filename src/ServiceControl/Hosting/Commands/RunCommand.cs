namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure.OWIN;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Persistence;
    using Hosting;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(args.ServiceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            settings.RunCleanupBundle = true;
            settings.RunAsWindowsService = args.RunAsWindowsService;

            var loggingSettings = new LoggingSettings(args.ServiceName);

            var bootstrapper = new Bootstrapper(settings, endpointConfiguration, loggingSettings);
            var hostBuilder = bootstrapper.HostBuilder;

            using var app = hostBuilder.Build();

            // TODO move these into central class to re-use?
            app.UseResponseCompression();
            app.UseMiddleware<BodyUrlRouteFix>();
            app.UseMiddleware<LogApiCalls>();
            app.MapHub<MessageStreamerHub>("/api/messagestream");
            app.UseCors();
            app.UseRouting();
            app.MapControllers();

            // Initialized IDocumentStore, this is needed as many hosted services have (indirect) dependencies on it.
            await app.Services.GetRequiredService<IPersistenceLifecycle>().Initialize();
            await app.RunAsync(settings.RootUrl);
        }
    }
}
