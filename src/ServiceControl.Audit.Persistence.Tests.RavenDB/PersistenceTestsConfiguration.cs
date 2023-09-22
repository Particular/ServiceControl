namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using global::Raven.Client;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.RavenDb;
    using TestHelper;
    using UnitOfWork;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }
        public IFailedAuditStorage FailedAuditStorage { get; protected set; }
        public IBodyStorage BodyStorage { get; protected set; }
        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; protected set; }
        ServiceProvider serviceProvider;

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new RavenDbPersistenceConfiguration();
            var serviceCollection = new ServiceCollection();

            var settings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            settings.PersisterSpecificSettings["RavenDB35/RunInMemory"] = bool.TrueString;
            settings.PersisterSpecificSettings["DatabaseMaintenancePort"] = PortUtility.FindAvailablePort(33334).ToString();
            settings.PersisterSpecificSettings["HostName"] = "localhost";

            setSettings(settings);

            var persistence = config.Create(settings);
            persistenceLifecycle = persistence.Configure(serviceCollection);

            await persistenceLifecycle.Start();

            serviceProvider = serviceCollection.BuildServiceProvider();

            AuditDataStore = serviceProvider.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = serviceProvider.GetRequiredService<IFailedAuditStorage>();
            DocumentStore = serviceProvider.GetRequiredService<IDocumentStore>();
            BodyStorage = serviceProvider.GetRequiredService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = serviceProvider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
        }

        public Task CompleteDBOperation()
        {
            DocumentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        public async Task Cleanup()
        {
            await persistenceLifecycle.Stop();
            await serviceProvider.DisposeAsync();

        }

        IPersistenceLifecycle persistenceLifecycle;

        public IDocumentStore DocumentStore { get; private set; }

        public string Name => "RavenDB";
    }
}