namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Threading.Channels;
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

            // Body storage and writer - register based on configuration
            RegisterBodyStorage(services, settings);

            // Unit of work for audit ingestion
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, MongoAuditIngestionUnitOfWorkFactory>();

            // Failed audit storage
            services.AddSingleton<IFailedAuditStorage, MongoFailedAuditStorage>();

            // Audit data store for queries
            services.AddSingleton<IAuditDataStore, MongoAuditDataStore>();

            // Storage pressure monitoring - checked by CheckMongoDbCachePressure (auto-loaded by NServiceBus)
            services.AddSingleton<MinimumRequiredStorageState>();
        }

        public void AddInstaller(IServiceCollection services) => ConfigureLifecycle(services, settings);

        static void RegisterBodyStorage(IServiceCollection services, MongoSettings settings)
        {
            switch (settings.BodyStorageType)
            {
                case BodyStorageType.None:
                    services.AddSingleton<NullBodyStorage>();
                    services.AddSingleton<IBodyStorage>(sp => sp.GetRequiredService<NullBodyStorage>());
                    services.AddSingleton<IBodyWriter>(sp => sp.GetRequiredService<NullBodyStorage>());
                    break;

                case BodyStorageType.Database:
                    RegisterBodyWriteChannel(services);
                    services.AddSingleton<MongoBodyStorage>();
                    services.AddSingleton<IBodyStorage>(sp => sp.GetRequiredService<MongoBodyStorage>());
                    services.AddSingleton<IBodyWriter>(sp => sp.GetRequiredService<MongoBodyStorage>());
                    services.AddHostedService(sp => sp.GetRequiredService<MongoBodyStorage>());
                    break;

                case BodyStorageType.Blob:
                    RegisterBodyWriteChannel(services);
                    services.AddSingleton<AzureBlobBodyStorage>();
                    services.AddSingleton<IBodyStorage>(sp => sp.GetRequiredService<AzureBlobBodyStorage>());
                    services.AddSingleton<IBodyWriter>(sp => sp.GetRequiredService<AzureBlobBodyStorage>());
                    services.AddHostedService(sp => sp.GetRequiredService<AzureBlobBodyStorage>());
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.BodyStorageType), settings.BodyStorageType, "Unknown body storage type");
            }
        }

        static void RegisterBodyWriteChannel(IServiceCollection services)
        {
            var channel = Channel.CreateBounded<BodyWriteItem>(new BoundedChannelOptions(10_000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            services.AddSingleton(channel);
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
