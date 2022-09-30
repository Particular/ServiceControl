﻿namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Logging;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents;
    using ServiceControl.Audit.Persistence.RavenDb5;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var documentStore = InitializeDatabase(settings, false)
                .GetAwaiter().GetResult();

            serviceCollection.AddSingleton(documentStore);

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
        }

        public Task Setup(PersistenceSettings settings)
        {
            return InitializeDatabase(settings, true);
        }


        Task<IDocumentStore> InitializeDatabase(PersistenceSettings settings, bool isSetup)
        {
            var useEmbeddedInstance = false;
            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/UseEmbeddedInstance", out var useEmbeddedInstanceString))
            {
                useEmbeddedInstance = bool.Parse(useEmbeddedInstanceString);
            }

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);

            if (useEmbeddedInstance)
            {
                var dbPath = settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"];
                var hostName = settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"];
                var databaseMaintenancePort = int.Parse(settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"]);
                var databaseMaintenanceUrl = $"http://{hostName}:{databaseMaintenancePort}";

                embeddedRavenDb = EmbeddedDatabase.Start(dbPath, expirationProcessTimerInSeconds, databaseMaintenanceUrl, settings.EnableFullTextSearchOnBodies, isSetup);
            }
            else
            {
                var connectionString = settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/ConnectionString"];

                embeddedRavenDb = new EmbeddedDatabase(expirationProcessTimerInSeconds, connectionString, useEmbeddedInstance, settings.EnableFullTextSearchOnBodies);
            }

            if (!settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/DatabaseName", out var databaseName))
            {
                databaseName = "audit";
            }

            return embeddedRavenDb.PrepareDatabase(new AuditDatabaseConfiguration(databaseName), isSetup);
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
