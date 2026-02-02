namespace ServiceControl.Audit.Persistence.MongoDB
{
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using UnitOfWork;

    class MongoPersistence(MongoSettings settings) : IPersistence
    {
        public void AddPersistence(IServiceCollection services)
        {
            ConfigureLifecycle(services, settings);

            // Register product capabilities - will be populated during initialization
            services.AddSingleton(sp =>
                sp.GetRequiredService<IMongoClientProvider>().ProductCapabilities);

            // Stage 2 - Unit of work for audit ingestion
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, MongoAuditIngestionUnitOfWorkFactory>();

            // Stage 6 - Failed audit storage
            services.AddSingleton<IFailedAuditStorage, MongoFailedAuditStorage>();

            // Stage 4 - Body storage (base64 in separate collection)
            services.AddSingleton<IBodyStorage, MongoBase64BodyStorage>();

            // TODO: Stage 3 - Add IAuditDataStore
            // TODO: Stage 7 - Add MinimumRequiredStorageState
        }

        public void AddInstaller(IServiceCollection services) => ConfigureLifecycle(services, settings);

        static void ConfigureLifecycle(IServiceCollection services, MongoSettings settings)
        {
            services.AddSingleton(settings);

            services.AddSingleton<MongoClientProvider>();
            services.AddSingleton<IMongoClientProvider>(sp => sp.GetRequiredService<MongoClientProvider>());

            services.AddSingleton<MongoPersistenceLifecycle>();
            services.AddSingleton<IMongoPersistenceLifecycle>(sp => sp.GetRequiredService<MongoPersistenceLifecycle>());

            services.AddHostedService<MongoPersistenceLifecycleHostedService>();
        }
    }
}
