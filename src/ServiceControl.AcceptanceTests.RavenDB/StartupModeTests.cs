namespace ServiceControl.AcceptanceTests.RavenDB
{
    using System;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Hosting.Commands;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
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
            var transportIntegration = new ConfigureEndpointLearningTransport();

            settings = new Settings(
                transportType: transportIntegration.TypeName,
                forwardErrorMessages: false,
                errorRetentionPeriod: TimeSpan.FromDays(1),
                persisterType: "RavenDB")
            {
                TransportConnectionString = transportIntegration.ConnectionString,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
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
            hostBuilder.Services.AddPersistence(settings, maintenanceMode: true);

            using var host = hostBuilder.Build();
            await host.StartAsync();
            await host.StopAsync();
        }

        [Test]
        public async Task CanRunImportFailedMessagesMode()
            => await new ImportFailedErrorsCommand().Execute(new HostArguments([]), settings);

        [Test]
        public async Task ImportFailedErrorsHostCanActivateAllMessageHandlers()
        {
            // The import host consumes the instance's regular input queue, so pending recoverability
            // commands (e.g. ArchiveMessage from a bulk archive) can arrive while it runs. Every
            // handler of the RecoverabilityComponent the host registers must therefore be activatable.
            var handlerTypes = typeof(ImportFailedErrorsCommand).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && type.GetInterfaces().Any(
                    i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleMessages<>)))
                .Where(type => type.Namespace!.StartsWith("ServiceControl.Recoverability") ||
                               type.Namespace.StartsWith("ServiceControl.MessageFailures"))
                .OrderBy(type => type.FullName)
                .ToArray();

            Assert.That(handlerTypes, Is.Not.Empty);

            using var host = ImportFailedErrorsCommand.BuildHost(settings);
            await host.StartAsync();
            try
            {
                await using var scope = host.Services.CreateAsyncScope();
                Assert.Multiple(() =>
                {
                    foreach (var handlerType in handlerTypes)
                    {
                        // NServiceBus activates handlers via ActivatorUtilities, resolving constructor
                        // dependencies from the container without the handler type being registered.
                        Assert.That(() => ActivatorUtilities.CreateInstance(scope.ServiceProvider, handlerType), Throws.Nothing, handlerType.FullName);
                    }
                });
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}