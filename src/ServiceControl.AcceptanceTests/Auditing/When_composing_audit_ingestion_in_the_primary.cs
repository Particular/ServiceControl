namespace ServiceControl.AcceptanceTests.Auditing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.LicensingComponent.AuditThroughput;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing;
    using ServiceControl.Connection;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence;
    using ServiceControl.SagaAudit;

    class When_composing_audit_ingestion_in_the_primary : AcceptanceTest
    {
        [Test]
        public async Task Should_host_the_audit_runtime()
        {
            var (app, services) = await BuildHost();

            try
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(HostsAuditIngestion(services), Is.True);
                    Assert.That(app.Services.GetService<AuditIngestor>(), Is.Not.Null);
                    Assert.That(app.Services.GetService<ImportFailedAudits>(), Is.Not.Null);
                    Assert.That(app.Services.GetService<IFailedAuditImportDataStore>(), Is.Not.Null);
                    Assert.That(app.Services.GetService<IAuditCountsDataStore>(), Is.Not.Null);
                    Assert.That(app.Services.GetService<ISagaHistoryDataStore>(), Is.Not.Null);

                    Assert.That(app.Services.GetService<ILocalAuditSource>(), Is.Not.Null,
                        "without it the local audit queues are counted as customer endpoints in the licensing report");
                    Assert.That(services.Any(descriptor =>
                            descriptor.ServiceType == typeof(IProvidePlatformConnectionDetails)
                            && descriptor.ImplementationType == typeof(AuditPlatformConnectionDetailsProvider)),
                        Is.True,
                        "/api/connection must still tell endpoints where to send audit and saga data");
                }
            }
            finally
            {
                await app.DisposeAsync();
            }
        }

        [Test]
        public async Task Should_keep_every_audit_capability_but_the_receiver_when_ingestion_is_disabled()
        {
            var (app, services) = await BuildHost(settings => settings.IngestAuditMessages = false);

            try
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(HostsAuditIngestion(services), Is.False,
                        "the receiver is the only thing the setting turns off, because other processes may still be ingesting");
                    Assert.That(app.Services.GetService<AuditIngestor>(), Is.Not.Null);
                    Assert.That(app.Services.GetService<IAuditCountsDataStore>(), Is.Not.Null);
                }
            }
            finally
            {
                await app.DisposeAsync();
            }
        }

        // The registrations are inspected rather than resolved. A normal primary hosts an NServiceBus
        // endpoint, and constructing every hosted service without starting it fails inside the
        // transport's receive component.
        static bool HostsAuditIngestion(IServiceCollection services) =>
            services.Any(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(AuditIngestion));

        async Task<(WebApplication App, IServiceCollection Services)> BuildHost(Action<Settings> customize = null)
        {
            var settings = await CreateSettings();

            customize?.Invoke(settings);

            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            endpointConfiguration.AssemblyScanner().Disable = true;

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            return (hostBuilder.Build(), hostBuilder.Services);
        }

        async Task<Settings> CreateSettings()
        {
            var settings = new Settings(TransportIntegration.TypeName, StorageConfiguration.PersistenceType,
                CreateLoggingSettings(), forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                InstanceName = $"AuditComposition.{Guid.NewGuid():n}",
                TransportConnectionString = TransportIntegration.ConnectionString,
                MaximumConcurrencyLevel = 2,
                DisableHealthChecks = true,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            await StorageConfiguration.CustomizeSettings(settings);

            return settings;
        }

        static LoggingSettings CreateLoggingSettings()
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);
            return new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath);
        }
    }
}
