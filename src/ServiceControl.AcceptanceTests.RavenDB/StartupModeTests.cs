namespace ServiceControl.AcceptanceTests.RavenDB
{
    using System;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Hosting.Commands;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;


    class StartupModeTests : AcceptanceTest
    {
        Settings settings;
        AcceptanceTestStorageConfiguration configuration;

        [SetUp]
        public async Task InitializeSettings()
        {
            PersistenceFactory.AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default;

            var transportIntegration = new ConfigureEndpointLearningTransport();

            settings = new Settings
            {
                ServiceControl =
                {
                    TransportType = transportIntegration.TypeName,
                    ErrorRetentionPeriod = TimeSpan.FromDays(1),
                    PersistenceType = "RavenDB",
                    ConnectionString = transportIntegration.ConnectionString,
                }
            };

            configuration = new AcceptanceTestStorageConfiguration();
            await configuration.CustomizeSettings(settings);
        }

        [TearDown]
        public async Task Cleanup() => await configuration.Cleanup();

        [Test]
        public async Task CanRunMaintenanceMode()
        {
            // ideally we'd be using the MaintenanceModeCommand here but that indefinitely blocks due to the RunAsync
            // not terminating.
            var hostBuilder = Host.CreateApplicationBuilder();
            settings.ServiceControl.MaintenanceMode = true;
            hostBuilder.Services.AddPersistence(hostBuilder.Configuration); // TODO: Configuration needs to be initialized

            using var host = hostBuilder.Build();
            await host.StartAsync();
            await host.StopAsync();
        }

        [Test]
        public async Task CanRunImportFailedMessagesMode()
            => await new TestableImportFailedErrorsCommand().Execute(new HostArguments([], maintenanceMode: false));

        class TestableImportFailedErrorsCommand() : ImportFailedErrorsCommand()
        {
            protected override EndpointConfiguration CreateEndpointConfiguration(Settings settings)
            {
                var configuration = base.CreateEndpointConfiguration(settings);

                //HINT: we want to exclude this assembly to prevent loading features that are part of the acceptance testing framework
                var thisAssembly = new[]
                {
                    typeof(StartupModeTests).Assembly.GetName().Name
                };

                configuration.AssemblyScanner().ExcludeAssemblies(thisAssembly);

                return configuration;
            }
        }
    }
}