namespace ServiceControl.AcceptanceTests.Auditing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing;
    using ServiceControl.Auditing.Reporting;
    using ServiceControl.CustomChecks;
    using Particular.ServiceControl.Hosting;
    using ServiceControl.Hosting.Commands;
    using ServiceControl.Infrastructure;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.SagaAudit;

    class When_hosting_an_audit_instance : AcceptanceTest
    {
        [Test]
        public async Task Should_own_its_database_serve_the_api_and_report_to_the_primary()
        {
            var settings = await CreateSettings();

            var host = AuditInstanceCommand.BuildHost(settings);

            try
            {
                var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType().Name).ToArray();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(hostedServices, Does.Contain(nameof(AuditIngestion)), "the host ingests the audit queue");
                    Assert.That(hostedServices, Does.Not.Contain(nameof(ErrorIngestion)), "and never the error queue");
                    Assert.That(hostedServices, Does.Contain("RetentionSweeper"), "it owns retention of its own database");
                    Assert.That(host.Services.GetService<IRetentionSweeper>(), Is.Not.Null);

                    Assert.That(host.Services.GetService<IMessageSession>(), Is.Not.Null, "the send only endpoint it reports through");
                    Assert.That(host.Services.GetService<ICustomCheckResultReporter>(), Is.TypeOf<PrimaryCustomCheckResultReporter>());
                    Assert.That(host.Services.GetService<IEndpointDetectionReporter>(), Is.TypeOf<PrimaryEndpointDetectionReporter>());

                    Assert.That(host.Services.GetService<GetSagaByIdApi>(), Is.Not.Null, "the primary's scatter gather calls this API");
                    Assert.That(host.Services.GetService<IFailedAuditImportDataStore>(), Is.Not.Null);
                    Assert.That(host.Services.GetService<ImportFailedAudits>(), Is.Not.Null);
                    Assert.That(host.Services.GetService<IDatabaseMigrator>(), Is.Null, "the run host never migrates; --setup does");
                }
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        // Setup runs through the same flag as the host, which is what provisions the audit database,
        // the audit queue and body storage for it.
        [Test]
        public async Task Should_provision_with_setup_and_start_with_its_send_only_endpoint()
        {
            var settings = await CreateSettings();

            await new SetupCommand().Execute(new HostArguments(["--setup", "--audit-instance"]), settings);

            var host = AuditInstanceCommand.BuildHost(settings);
            host.Urls.Add("http://127.0.0.1:0");

            var started = false;

            try
            {
                await host.StartAsync();
                started = true;

                var session = host.Services.GetRequiredService<IMessageSession>();

                Assert.DoesNotThrowAsync(() => host.Services.GetRequiredService<IEndpointDetectionReporter>().Report(
                    [new EndpointDetails { Name = "Sales", Host = "host", HostId = Guid.NewGuid() }]));
                Assert.That(session, Is.Not.Null);
            }
            finally
            {
                if (started)
                {
                    await host.StopAsync();
                }

                await host.DisposeAsync();
            }
        }

        async Task<Settings> CreateSettings()
        {
            var settings = new Settings(TransportIntegration.TypeName, StorageConfiguration.PersistenceType,
                CreateLoggingSettings(), forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                InstanceName = $"AuditInstance.{Guid.NewGuid():n}",
                TransportConnectionString = TransportIntegration.ConnectionString,
                MaximumConcurrencyLevel = 2,
                DisableHealthChecks = true,
                ServiceControlQueueAddress = $"Primary.{Guid.NewGuid():n}",
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
