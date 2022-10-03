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
            var persistenceLifeCycle = CreatePersistenceLifecycle(settings);

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IRavenDbSessionProvider, RavenDbSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
            serviceCollection.AddSingleton<IRavenDbDocumentStoreProvider>(_ => persistenceLifeCycle);

            return persistenceLifeCycle;
        }

        public async Task Setup(PersistenceSettings settings, CancellationToken cancellationToken = default)
        {
            var persistenceLifeCycle = CreatePersistenceLifecycle(settings);

            await persistenceLifeCycle.Start(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                using (var documentStore = persistenceLifeCycle.GetDocumentStore())
                {
                    var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);
                    var dataBaseConfiguration = GetDatabaseConfiguration(settings);
                    var databaseSetup = new DatabaseSetup(expirationProcessTimerInSeconds, settings.EnableFullTextSearchOnBodies, dataBaseConfiguration);

                    await databaseSetup.Execute(documentStore, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await persistenceLifeCycle.Stop(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        IRavenDbPersistenceLifecycle CreatePersistenceLifecycle(PersistenceSettings settings)
        {
            var dataBaseConfiguration = GetDatabaseConfiguration(settings);

            if (UseEmbeddedInstance(settings))
            {
                var dbPath = settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"];
                var hostName = settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"];
                var databaseMaintenancePort = int.Parse(settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"]);
                var databaseMaintenanceUrl = $"http://{hostName}:{databaseMaintenancePort}";

                var database = EmbeddedDatabase.Start(dbPath, databaseMaintenanceUrl, dataBaseConfiguration);
                var embeddedPersistenceLifecycle = new RavenDbEmbeddedPersistenceLifecycle(database);

                return embeddedPersistenceLifecycle;
            }

            var connectionString = settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/ConnectionString"];

            return new RavenDbExternalPersistenceLifecycle(connectionString, dataBaseConfiguration);
        }

        static AuditDatabaseConfiguration GetDatabaseConfiguration(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/DatabaseName", out var databaseName))
            {
                databaseName = "audit";
            }

            return new AuditDatabaseConfiguration(databaseName);
        }

        static bool UseEmbeddedInstance(PersistenceSettings settings)
        {
            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/UseEmbeddedInstance", out var useEmbeddedInstanceString))
            {
                return bool.Parse(useEmbeddedInstanceString);
            }

            return false;
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
