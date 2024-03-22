namespace ServiceControl.Audit.Persistence.InMemory
{
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using UnitOfWork;

    public class InMemoryPersistence(PersistenceSettings persistenceSettings) : IPersistence
    {
        public void Configure(IServiceCollection services)
        {
            services.AddSingleton(persistenceSettings);
            services.AddSingleton<InMemoryAuditDataStore>();
            services.AddSingleton<IAuditDataStore>(sp => sp.GetRequiredService<InMemoryAuditDataStore>());
            services.AddSingleton<IBodyStorage, InMemoryAttachmentsBodyStorage>();
            services.AddSingleton<IFailedAuditStorage, InMemoryFailedAuditStorage>();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, InMemoryAuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<IPersistenceLifecycle, InMemoryPersistenceLifecycle>();
        }

        public IPersistenceInstaller CreateInstaller() => new InMemoryPersistenceInstaller();
    }
}