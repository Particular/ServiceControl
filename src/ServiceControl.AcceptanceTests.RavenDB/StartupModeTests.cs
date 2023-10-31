namespace ServiceControl.AcceptanceTests.RavenDB
{
    using System;
    using System.Threading.Tasks;
    using Hosting.Commands;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using Persistence.RavenDB;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;


    class StartupModeTests : AcceptanceTest
    {
        Settings settings;
        DatabaseLease database;

        [SetUp]
        public void InitializeSettings()
        {
            var transportIntegration = new ConfigureEndpointLearningTransport();

            database = SharedDatabaseSetup.LeaseDatabase();

            settings = new Settings(
                forwardErrorMessages: false,
                errorRetentionPeriod: TimeSpan.FromDays(1),
                persisterType: typeof(RavenPersistenceConfiguration).AssemblyQualifiedName)
            {
                PersisterSpecificSettings = new RavenPersisterSettings
                {
                    ErrorRetentionPeriod = TimeSpan.FromDays(1),
                    ConnectionString = SharedDatabaseSetup.SharedInstance.ServerUrl,
                    DatabaseName = database.DatabaseName
                },
                TransportType = transportIntegration.TypeName,
                TransportConnectionString = transportIntegration.ConnectionString,

            };
        }

        [TearDown]
        public async Task Cleanup()
        {
            await database.DisposeAsync();
        }

        [Test]
        public async Task CanRunMaintenanceMode()
        {
            var bootstrapper = new MaintenanceBootstrapper(settings);

            var host = bootstrapper.HostBuilder.Build();

            await host.StartAsync();
            await host.StopAsync();
        }

        [Test]
        public async Task CanRunImportFailedMessagesMode()
        {
            await new TestableImportFailedErrorsCommand().Execute(new HostArguments(Array.Empty<string>()), settings);
        }

        class TestableImportFailedErrorsCommand : ImportFailedErrorsCommand
        {
            protected override EndpointConfiguration CreateEndpointConfiguration(Settings settings)
            {
                var configuration = base.CreateEndpointConfiguration(settings);

                //HINT: we want to exclude this assembly to prevent loading features that are part of the acceptance testing framework  
                var thisAssembly = new[] { typeof(StartupModeTests).Assembly.GetName().Name };

                configuration.AssemblyScanner().ExcludeAssemblies(thisAssembly);

                return configuration;
            }
        }
    }
}