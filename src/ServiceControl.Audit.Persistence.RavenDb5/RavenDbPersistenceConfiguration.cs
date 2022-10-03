namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Logging;
    using Persistence.UnitOfWork;
    using ServiceControl.Audit.Persistence.RavenDb5;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistenceLifecycle ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var database = CreateDatabase(settings);
            var persistenceLifecycle = new RavenDbPersistenceLifecycle(database);

            serviceCollection.AddSingleton(settings);

            serviceCollection.AddSingleton<IRavenDbDocumentStoreProvider>(_ => new RavenDbDocumentStoreProvider(persistenceLifecycle));
            serviceCollection.AddSingleton<IRavenDbSessionProvider, RavenDbSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();

            return persistenceLifecycle;
        }

        public async Task Setup(PersistenceSettings settings, CancellationToken cancellationToken = default)
        {
            using (var database = CreateDatabase(settings))
            {
                using (var documentStore = await database.Initialize(cancellationToken).ConfigureAwait(false))
                {
                    var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);
                    var dataBaseConfiguration = GetDatabaseConfiguration(settings);
                    var databaseSetup = new DatabaseSetup(expirationProcessTimerInSeconds, settings.EnableFullTextSearchOnBodies, dataBaseConfiguration);

                    await databaseSetup.Execute(documentStore, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        public static EmbeddedDatabase CreateDatabase(PersistenceSettings settings)
        {
            var useEmbeddedInstance = false;
            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/UseEmbeddedInstance", out var useEmbeddedInstanceString))
            {
                useEmbeddedInstance = bool.Parse(useEmbeddedInstanceString);
            }

            var dataBaseConfiguration = GetDatabaseConfiguration(settings);

            EmbeddedDatabase embeddedRavenDb;
            if (useEmbeddedInstance)
            {
                var dbPath = settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"];
                var hostName = settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"];
                var databaseMaintenancePort = int.Parse(settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"]);
                var databaseMaintenanceUrl = $"http://{hostName}:{databaseMaintenancePort}";

                embeddedRavenDb = EmbeddedDatabase.Start(dbPath, databaseMaintenanceUrl, dataBaseConfiguration);
            }
            else
            {
                var connectionString = settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/ConnectionString"];

                embeddedRavenDb = new EmbeddedDatabase(connectionString, useEmbeddedInstance, dataBaseConfiguration);
            }

            return embeddedRavenDb;
        }

        static AuditDatabaseConfiguration GetDatabaseConfiguration(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/DatabaseName", out var databaseName))
            {
                databaseName = "audit";
            }

            return new AuditDatabaseConfiguration(databaseName);
        }

        static int GetExpirationProcessTimerInSeconds(PersistenceSettings settings)
        {
            var expirationProcessTimerInSeconds = ExpirationProcessTimerInSecondsDefault;

            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl.Audit/ExpirationProcessTimerInSeconds", out var expirationProcessTimerInSecondsString))
            {
                expirationProcessTimerInSeconds = int.Parse(expirationProcessTimerInSecondsString);
            }

            if (expirationProcessTimerInSeconds < 0)
            {
                logger.Error($"ExpirationProcessTimerInSeconds cannot be negative. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                return ExpirationProcessTimerInSecondsDefault;
            }

            if (expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds)
            {
                logger.Error($"ExpirationProcessTimerInSeconds cannot be larger than {TimeSpan.FromHours(3).TotalSeconds}. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                return ExpirationProcessTimerInSecondsDefault;
            }

            return expirationProcessTimerInSeconds;
        }

        static ILog logger = LogManager.GetLogger(typeof(RavenDbPersistenceConfiguration));

        const int ExpirationProcessTimerInSecondsDefault = 600;
    }
}
