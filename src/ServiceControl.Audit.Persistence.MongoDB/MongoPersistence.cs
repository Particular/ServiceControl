namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using Auditing.BodyStorage;
    using BodyStorage;
    using Indexes;
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

            // Unit of work for audit ingestion
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, MongoAuditIngestionUnitOfWorkFactory>();

            // Failed audit storage
            services.AddSingleton<IFailedAuditStorage, MongoFailedAuditStorage>();

            // Body storage - register based on configuration
            RegisterBodyStorage(services, settings);

            // Audit data store for queries
            services.AddSingleton<IAuditDataStore, MongoAuditDataStore>();

            // TODO: Stage 7 - Add MinimumRequiredStorageState
        }

        public void AddInstaller(IServiceCollection services) => ConfigureLifecycle(services, settings);

        static void RegisterBodyStorage(IServiceCollection services, MongoSettings settings)
        {
            switch (settings.BodyStorageType)
            {
                case BodyStorageType.None:
                    services.AddSingleton<IBodyStorage, NullBodyStorage>();
                    break;

                case BodyStorageType.Database:
                    services.AddSingleton<IBodyStorage, InlineBodyStorage>();
                    break;

                case BodyStorageType.FileSystem:
                    services.AddSingleton<IBodyStorage, FileSystemBodyStorage>();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.BodyStorageType), settings.BodyStorageType, "Unknown body storage type");
            }
        }

        static void ConfigureLifecycle(IServiceCollection services, MongoSettings settings)
        {
            services.AddSingleton(settings);

            services.AddSingleton<MongoClientProvider>();
            services.AddSingleton<IMongoClientProvider>(sp => sp.GetRequiredService<MongoClientProvider>());

            services.AddSingleton<IndexInitializer>();

            services.AddSingleton<MongoPersistenceLifecycle>();
            services.AddSingleton<IMongoPersistenceLifecycle>(sp => sp.GetRequiredService<MongoPersistenceLifecycle>());

            services.AddHostedService<MongoPersistenceLifecycleHostedService>();
        }
    }
}
