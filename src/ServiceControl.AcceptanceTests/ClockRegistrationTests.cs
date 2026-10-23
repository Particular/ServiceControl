namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NUnit.Framework;
    using global::ServiceControl.Infrastructure.WebApi;
    using Particular.ServiceControl;
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

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.Services.AddSingleton(hostClock);
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            using var host = hostBuilder.Build();

            Assert.That(host.Services.GetRequiredService<TimeProvider>(), Is.SameAs(hostClock));
        }

        sealed class StubTimeProvider : TimeProvider;
    }
}
