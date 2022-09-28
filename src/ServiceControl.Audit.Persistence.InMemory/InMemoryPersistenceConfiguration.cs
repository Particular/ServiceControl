namespace ServiceControl.Audit.Persistence.InMemory
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<InMemoryAuditDataStore>();
            serviceCollection.AddSingleton<IAuditDataStore>(sp => sp.GetRequiredService<InMemoryAuditDataStore>());
            serviceCollection.AddSingleton<IBodyStorage, InMemoryAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IFailedAuditStorage, InMemoryFailedAuditStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, InMemoryAuditIngestionUnitOfWorkFactory>();
        }
    }
}
