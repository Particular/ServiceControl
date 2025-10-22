namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus;
    using Operations;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    class ImportFailedErrorsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.SetupApplicationConfiguration();
            hostBuilder.Services.Configure<PrimaryOptions>(s =>
            {
                s.IngestErrorMessages = false;
                s.RunRetryProcessor = false;
                s.DisableHealthChecks = true;
            });

            var settings = hostBuilder.Configuration.Get<Settings>(); // THIS WILL NOT RESOLVE SETTINGS WITH THE ABOVE VALUES !!!! TOOD: SHould really use a different type any settings used here.

            EndpointConfiguration endpointConfiguration = CreateEndpointConfiguration(settings);
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

            using var app = hostBuilder.Build();
            await app.StartAsync();

            var importFailedErrors = app.Services.GetRequiredService<ImportFailedErrors>();

            using var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, _) => tokenSource.Cancel();

            try
            {
                await importFailedErrors.Run(tokenSource.Token);
            }
            catch (OperationCanceledException e) when (tokenSource.IsCancellationRequested)
            {
                LoggerUtil.CreateStaticLogger<ImportFailedErrorsCommand>().LogInformation(e, "Cancelled");
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }


        protected virtual EndpointConfiguration CreateEndpointConfiguration(Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.ServiceControl.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            return endpointConfiguration;
        }
    }

    public static class HostBuilderExt
    {
        public static void SetupApplicationConfiguration(this IHostApplicationBuilder hostBuilder)
        {
            hostBuilder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddLegacyAppSettings()
                .AddEnvironmentVariables();

            hostBuilder.Services.AddOptions<PrimaryOptions>()
                .Bind(hostBuilder.Configuration.GetSection(PrimaryOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }
}