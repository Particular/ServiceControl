namespace ServiceControl.AcceptanceTests.RavenDB
{
    using System;
    using System.Threading.Tasks;
    using Hosting.Commands;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using Persistence.RavenDb;
    using ServiceBus.Management.Infrastructure.Settings;

    class StartupModeTests : AcceptanceTest
    {
        Settings settings;

        [SetUp]
        public void InitializeSettings()
        {
            settings = new Settings(
                forwardErrorMessages: false,
                errorRetentionPeriod: TimeSpan.FromDays(1),
                persisterType: typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName)
            {
                PersisterSpecificSettings = new RavenDBPersisterSettings
                {
                    ErrorRetentionPeriod = TimeSpan.FromDays(1),
                    RunInMemory = true
                },
                TransportType = TransportIntegration.TypeName,
                TransportConnectionString = TransportIntegration.ConnectionString
            };
        }

        [Test]
        public async Task CanRunMaintenanceMode()
        {
            var bootstrapper = new MaintenanceBootstrapper(settings);

            var host = bootstrapper.HostBuilder.Build();

            await host.StartAsync();
            await host.StopAsync();
        }

        //[Test]
        //public async Task CanRunImportFailedMessagesMode()
        //{
        //    await new ImportFailedErrorsCommand().Execute(new HostArguments(Array.Empty<string>()), settings);
        //}
    }
}