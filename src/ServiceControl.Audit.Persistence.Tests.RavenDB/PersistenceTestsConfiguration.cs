namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using global::Raven.Client;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.RavenDb;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration : PersistenceTestsConfigurationBase
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb35/RunInMemory"] = bool.TrueString;
            settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"] = FindAvailablePort(33334).ToString();
            settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"] = "localhost";

            setSettings(settings);

            persistenceLifecycle = config.ConfigureServices(serviceCollection, settings);

            await persistenceLifecycle.Start();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
        }

        public override Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public override Task Cleanup()
        {
            return persistenceLifecycle?.Stop();
        }

        IPersistenceLifecycle persistenceLifecycle;

        public IDocumentStore DocumentStore { get; private set; }

        public string Name => "RavenDb";

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }
    }
}