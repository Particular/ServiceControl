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
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Tests.AuditCapable;

    // The inner persistence type reaches the test persister through an environment variable, which is
    // process wide, so these cannot run alongside anything else that sets it.
    [NonParallelizable]
    class When_composing_audit_ingestion_in_the_primary : AcceptanceTest
    {
        [Test]
        public async Task Should_host_the_audit_runtime_when_the_persister_advertises_audit_support()
        {
            var (app, services) = await BuildHost(auditCapable: true);

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
            var (app, services) = await BuildHost(auditCapable: true, settings => settings.IngestAuditMessages = false);

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

        [Test]
        public async Task Should_host_nothing_audit_related_on_a_persister_without_audit_support()
        {
            var (app, services) = await BuildHost(auditCapable: false);

            try
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(HostsAuditIngestion(services), Is.False);
                    Assert.That(app.Services.GetService<AuditIngestor>(), Is.Null);
                    Assert.That(app.Services.GetService<AuditIngestionCustomCheck.State>(), Is.Null);
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

        async Task<(WebApplication App, IServiceCollection Services)> BuildHost(bool auditCapable, Action<Settings> customize = null)
        {
            var settings = await CreateSettings(auditCapable);

            customize?.Invoke(settings);

            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            endpointConfiguration.AssemblyScanner().Disable = true;

            var hostBuilder = WebApplication.CreateBuilder();
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            return (hostBuilder.Build(), hostBuilder.Services);
        }

        async Task<Settings> CreateSettings(bool auditCapable)
        {
            var persistenceType = StorageConfiguration.PersistenceType;

            if (auditCapable)
            {
                // The test persister delegates everything but the audit contracts to the real one, so the
                // host under test is the real host apart from the capability its manifest advertises.
                Environment.SetEnvironmentVariable(InnerPersistenceTypeVariable, persistenceType);
                persistenceType = AuditCapablePersistenceName;
            }

            var settings = new Settings(TransportIntegration.TypeName, persistenceType,
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

        [TearDown]
        public void ClearInnerPersistenceType() => Environment.SetEnvironmentVariable(InnerPersistenceTypeVariable, null);

        static LoggingSettings CreateLoggingSettings()
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);
            return new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath);
        }

        const string AuditCapablePersistenceName = "AuditCapableTest";

        static readonly string InnerPersistenceTypeVariable =
            AuditCapableTestPersistenceConfiguration.InnerPersistenceTypeSetting.ToUpperInvariant();
    }
}
