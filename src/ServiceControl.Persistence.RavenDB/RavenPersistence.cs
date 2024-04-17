namespace ServiceControl.Persistence.RavenDB
{
    using MessageFailures;
    using MessageRedirects;
    using Microsoft.Extensions.DependencyInjection;
    using NewFeature;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Operations.BodyStorage;
    using Operations.BodyStorage.RavenAttachments;
    using Persistence.MessageRedirects;
    using Persistence.NewFeature;
    using Persistence.Recoverability;
    using Recoverability;
    using SagaAudit;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;
    using ServiceControl.Persistence.RavenDB.CustomChecks;
    using ServiceControl.Recoverability;
    using UnitOfWork;

    class RavenPersistence(RavenPersisterSettings settings) : IPersistence
    {
        public void AddPersistence(IServiceCollection services)
        {
            ConfigureLifecycle(services);

            if (settings.MaintenanceMode)
            {
                return;
            }

            services.AddSingleton<INewFeatureDataStore, RavenNewFeatureDataStore>();

            services.AddSingleton<IServiceControlSubscriptionStorage, RavenSubscriptionStorage>();
            services.AddSingleton<ISubscriptionStorage>(p => p.GetRequiredService<IServiceControlSubscriptionStorage>());

            services.AddSingleton<IMonitoringDataStore, RavenMonitoringDataStore>();
            services.AddSingleton<ICustomChecksDataStore, RavenCustomCheckDataStore>();
            services.AddUnitOfWorkFactory<RavenIngestionUnitOfWorkFactory>();
            services.AddSingleton<ExpirationManager>();
            services.AddSingleton<MinimumRequiredStorageState>();
            services.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();

            services.AddSingleton<FailedMessageViewIndexNotifications>();
            services.AddSingleton<IFailedMessageViewIndexNotifications>(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());
            services.AddHostedService(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());

            services.AddSingleton<ExternalIntegrationRequestsDataStore>();
            services.AddSingleton<IExternalIntegrationRequestsDataStore>(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());
            services.AddHostedService(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());

            services.AddCustomCheck<CheckRavenDBIndexErrors>();
            services.AddCustomCheck<CheckRavenDBIndexLag>();
            services.AddCustomCheck<CheckFreeDiskSpace>();
            services.AddCustomCheck<CheckMinimumStorageRequiredForIngestion>();

            services.AddSingleton<OperationsManager>();

            services.AddSingleton<IArchiveMessages, MessageArchiver>();
            services.AddSingleton<ICustomChecksDataStore, RavenCustomCheckDataStore>();
            services.AddSingleton<IErrorMessageDataStore, ErrorMessagesDataStore>();
            services.AddSingleton<IEventLogDataStore, EventLogDataStore>();
            services.AddSingleton<IFailedErrorImportDataStore, FailedErrorImportDataStore>();
            services.AddSingleton<IGroupsDataStore, GroupsDataStore>();
            services.AddSingleton<IGroupsDataStore, GroupsDataStore>();
            services.AddSingleton<IMessageRedirectsDataStore, MessageRedirectsDataStore>();
            services.AddSingleton<IMonitoringDataStore, RavenMonitoringDataStore>();
            services.AddSingleton<IQueueAddressStore, QueueAddressStore>();
            services.AddSingleton<IRetryBatchesDataStore, RetryBatchesDataStore>();
            services.AddSingleton<IRetryDocumentDataStore, RetryDocumentDataStore>();
            services.AddSingleton<IRetryHistoryDataStore, RetryHistoryDataStore>();

            // Forward saga audit messages and warn in ServiceControl 5, remove in 6
            services.AddSingleton<ISagaAuditDataStore, SagaAuditDeprecationDataStore>();
            services.AddCustomCheck<SagaAuditDestinationCustomCheck>();
            services.AddSingleton<SagaAuditDestinationCustomCheck.State>();
        }

        public void AddInstaller(IServiceCollection services) => ConfigureLifecycle(services);

        void ConfigureLifecycle(IServiceCollection services)
        {
            services.AddSingleton<PersistenceSettings>(settings);
            services.AddSingleton(settings);

            services.AddSingleton<IRavenSessionProvider, RavenSessionProvider>();
            services.AddHostedService<RavenPersistenceLifecycleHostedService>();

            if (settings.UseEmbeddedServer)
            {
                services.AddSingleton<RavenEmbeddedPersistenceLifecycle>();
                services.AddSingleton<IRavenPersistenceLifecycle>(b => b.GetService<RavenEmbeddedPersistenceLifecycle>());
                services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
                return;
            }

            services.AddSingleton<RavenExternalPersistenceLifecycle>();
            services.AddSingleton<IRavenPersistenceLifecycle>(b => b.GetService<RavenExternalPersistenceLifecycle>());
            services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
        }
    }
}