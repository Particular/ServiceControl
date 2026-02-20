namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.MongoDB;
    using UnitOfWork;

    class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; private set; }

        public IFailedAuditStorage FailedAuditStorage { get; private set; }

        public IBodyStorage BodyStorage { get; private set; }

        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; private set; }

        public IMongoDatabase MongoDatabase { get; private set; }

        public IServiceProvider ServiceProvider => host.Services;

        public string Name => "MongoDB";

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            var config = new MongoPersistenceConfiguration();
            var hostBuilder = Host.CreateApplicationBuilder();
            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);

            setSettings(persistenceSettings);

            // Get or start the shared MongoDB container
            _ = await SharedMongoDbContainer.GetInstance().ConfigureAwait(false);
            var baseConnectionString = SharedMongoDbContainer.GetConnectionString();

            // Use a unique database name per test to ensure isolation
            databaseName = $"test_{Guid.NewGuid():N}";
            var builder = new MongoUrlBuilder(baseConnectionString)
            {
                DatabaseName = databaseName
            };

            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] =
                builder.ToString();

            var persistence = config.Create(persistenceSettings);
            persistence.AddPersistence(hostBuilder.Services);
            persistence.AddInstaller(hostBuilder.Services);

            var assembly = typeof(MongoPersistenceConfiguration).Assembly;

            foreach (var type in assembly.DefinedTypes)
            {
                if (type.IsAssignableTo(typeof(ICustomCheck)))
                {
                    hostBuilder.Services.AddTransient(typeof(ICustomCheck), type);
                }
            }

            host = hostBuilder.Build();
            await host.StartAsync().ConfigureAwait(false);

            AuditDataStore = host.Services.GetRequiredService<IAuditDataStore>();
            FailedAuditStorage = host.Services.GetRequiredService<IFailedAuditStorage>();
            BodyStorage = host.Services.GetRequiredService<IBodyStorage>();
            AuditIngestionUnitOfWorkFactory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
            MongoDatabase = clientProvider.Database;
        }

        public Task CompleteDBOperation()
        {
            // MongoDB doesn't have deferred index updates like RavenDB
            // Operations are immediately visible after write acknowledgment
            return Task.CompletedTask;
        }

        public async Task Cleanup()
        {
            if (MongoDatabase != null)
            {
                // Drop the test database to clean up
                await MongoDatabase.Client.DropDatabaseAsync(databaseName).ConfigureAwait(false);
            }

            if (host != null)
            {
                await host.StopAsync().ConfigureAwait(false);
                host.Dispose();
            }
        }

        string databaseName;
        IHost host;
    }
}
