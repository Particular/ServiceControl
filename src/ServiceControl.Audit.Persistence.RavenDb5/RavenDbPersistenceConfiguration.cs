namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using UnitOfWork;
    using ServiceControl.Audit.Persistence.RavenDb5;
    using Raven.Embedded;
    using Raven.Client.Documents;
    using System;
    using NServiceBus.Logging;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var documentStore = InitializeDatabase(settings);

            serviceCollection.AddSingleton(documentStore);

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
        }

        IDocumentStore InitializeDatabase(PersistenceSettings settings)
        {
            var runInMemory = false;
            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/RunInMemory", out var runInMemoryString))
            {
                runInMemory = bool.Parse(runInMemoryString);
            }

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);

            if (ShouldStartServer(runInMemory))
            {
                var dbPath = settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"];
                var hostName = settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"];
                var databaseMaintenancePort = int.Parse(settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"]);
                var databaseMaintenanceUrl = $"http://{hostName}:{databaseMaintenancePort}";

                embeddedRavenDb = EmbeddedDatabase.Start(dbPath, expirationProcessTimerInSeconds, databaseMaintenanceUrl, settings.EnableFullTextSearchOnBodies);
            }
            else
            {
                var connectionString = settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/ConnectionString"];

                embeddedRavenDb = new EmbeddedDatabase(expirationProcessTimerInSeconds, connectionString, runInMemory, settings.EnableFullTextSearchOnBodies);
            }

            if (!settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/DatabaseName", out var databaseName))
            {
                databaseName = "audit";
            }

            return embeddedRavenDb.PrepareDatabase(new AuditDatabaseConfiguration(databaseName), settings.IsSetup).GetAwaiter().GetResult();
        }

        static bool ShouldStartServer(bool runInMemory)
        {
            if (runInMemory)
            {
                // We are probably running in a test context
                try
                {
                    EmbeddedServer.Instance.GetServerUriAsync().Wait();
                    // Embedded server is already running so we don't need to start it
                    return false;
                }
                catch
                {
                    // Embedded Server is not running
                    return true;
                }
            }

            return true;
        }
        int GetExpirationProcessTimerInSeconds(PersistenceSettings settings)
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

        EmbeddedDatabase embeddedRavenDb;

        ILog logger = LogManager.GetLogger(typeof(RavenDbPersistenceConfiguration));

        const int ExpirationProcessTimerInSecondsDefault = 600;
    }
}
