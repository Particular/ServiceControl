namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Auditing.BodyStorage;
    using Infrastructure.Settings;
    using Raven.Client.Documents;
    using UnitOfWork;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            serviceCollection.AddPersistenceLifecycle<RavenDbPersistenceLifecycle>();
            serviceCollection.AddSingleton<DeferredRavenDocumentStore>();
            serviceCollection.AddSingleton<IDocumentStore>(sp => sp.GetRequiredService<DeferredRavenDocumentStore>());

            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();
        }
    }
}
