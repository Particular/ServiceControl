﻿namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Infrastructure.Migration;
    using ServiceControl.Audit.Persistence.RavenDB;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, IDictionary<string, string> settings, bool maintenanceMode, bool isSetup)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings, maintenanceMode);

            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            if (isSetup)
            {
                var ravenOptions = new RavenStartup();
                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    ravenOptions.AddIndexAssembly(indexAssembly);
                }

                var embeddedRaven = new EmbeddedRavenDbHostedService(documentStore, ravenOptions, new IDataMigration[0]);
                embeddedRaven.SetupDatabase().GetAwaiter().GetResult();
            }
            else
            {
                serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
            }

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();

            serviceCollection.Configure<RavenStartup>(database =>
            {
                foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                {
                    database.AddIndexAssembly(indexAssembly);
                }
            });

            if (isSetup)
            {
                serviceCollection.AddTransient<IDataMigration, MigrateKnownEndpoints>();
            }
        }
    }
}
