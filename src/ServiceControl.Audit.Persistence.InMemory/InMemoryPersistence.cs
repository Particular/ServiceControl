namespace ServiceControl.Audit.Persistence.InMemory
{
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using UnitOfWork;

    public class InMemoryPersistence : IPersistence
    {
        public InMemoryPersistence(PersistenceSettings persistenceSettings) => settings = persistenceSettings;

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<InMemoryAuditDataStore>();
            serviceCollection.AddSingleton<IAuditDataStore>(sp => sp.GetRequiredService<InMemoryAuditDataStore>());
            serviceCollection.AddSingleton<IBodyStorage, InMemoryAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IFailedAuditStorage, InMemoryFailedAuditStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, InMemoryAuditIngestionUnitOfWorkFactory>();

            return new InMemoryPersistenceLifecycle();
        }

        public IPersistenceInstaller CreateInstaller() => new InMemoryPersistenceInstaller();

        readonly PersistenceSettings settings;
    }
}