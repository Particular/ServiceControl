namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using global::Raven.Client;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.RavenDb;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }

        public Task Configure()
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var specificSettings = new Dictionary<string, string>()
            {
                { "RavenDb/RunInMemory",bool.TrueString},
                { "RavenDb/MaintenancePort",FindAvailablePort(33334).ToString()},
                { "RavenDb/Hostname","localhost"}
            };

            var settings = new PersistenceSettings(specificSettings)
            {
                IsSetup = true
            };

            config.ConfigureServices(serviceCollection, settings);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            BodyStorage = serviceProvider.GetService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            return Task.CompletedTask;
        }

        public Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            DocumentStore?.Dispose();
            return Task.CompletedTask;
        }

        public override string ToString() => "RavenDb";

        public IDocumentStore DocumentStore { get; private set; }

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