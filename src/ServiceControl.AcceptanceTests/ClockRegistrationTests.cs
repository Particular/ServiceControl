namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    class ClockRegistrationTests : AcceptanceTest
    {
        Settings settings;

        [SetUp]
        public async Task InitializeSettings()
        {
            settings = new Settings(
                transportType: TransportIntegration.TypeName,
                persisterType: StorageConfiguration.PersistenceType,
                forwardErrorMessages: false,
                errorRetentionPeriod: TimeSpan.FromDays(1))
            {
                TransportConnectionString = TransportIntegration.ConnectionString,
                IngestErrorMessages = false,
                RunRetryProcessor = false,
                DisableHealthChecks = true,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            await StorageConfiguration.CustomizeSettings(settings);
        }

        [Test]
        public void Should_keep_the_clock_the_host_registered()
        {
            TimeProvider hostClock = new StubTimeProvider();

            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            endpointConfiguration.AssemblyScanner().Disable = true;

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddSingleton(hostClock);
            hostBuilder.AddServiceControl(settings, endpointConfiguration);

            using var host = hostBuilder.Build();

            Assert.That(host.Services.GetRequiredService<TimeProvider>(), Is.SameAs(hostClock));
        }

        [Test]
        public async Task Should_register_a_clock_in_a_persistence_only_host()
        {
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddPersistence(settings, maintenanceMode: true);

            using var host = hostBuilder.Build();
            await host.StartAsync();

            Assert.That(host.Services.GetRequiredService<TimeProvider>(), Is.Not.Null);

            await host.StopAsync();
        }

        sealed class StubTimeProvider : TimeProvider;
    }
}
