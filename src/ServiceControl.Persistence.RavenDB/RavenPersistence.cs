namespace ServiceControl.Persistence.RavenDB
{
    using MessageRedirects;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Persistence.Recoverability;
    using Recoverability;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;
    using ServiceControl.Persistence.MessageRedirects;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.Recoverability;
    using ServiceControl.SagaAudit;

    class RavenPersistence(RavenPersisterSettings settings) : IPersistence
    {
        public void Configure(IServiceCollection services)
        {
            services.AddSingleton<PersistenceSettings>(settings);
            services.AddSingleton(settings);

            ConfigureLifecycle(services);

            if (settings.MaintenanceMode)
            {
                return;
            }

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

        void ConfigureLifecycle(IServiceCollection serviceCollection)
        {
            if (settings.UseEmbeddedServer)
            {
                serviceCollection.AddSingleton<RavenEmbeddedPersistenceLifecycle>();
                serviceCollection.AddSingleton<IPersistenceLifecycle>(b => b.GetService<RavenEmbeddedPersistenceLifecycle>());
                serviceCollection.AddSingleton(b => b.GetService<RavenEmbeddedPersistenceLifecycle>().GetDocumentStore());
                return;
            }

            serviceCollection.AddSingleton<RavenExternalPersistenceLifecycle>();
            serviceCollection.AddSingleton<IPersistenceLifecycle>(b => b.GetService<RavenExternalPersistenceLifecycle>());
            serviceCollection.AddSingleton(b => b.GetService<RavenExternalPersistenceLifecycle>().GetDocumentStore());
        }

        public IPersistenceInstaller CreateInstaller()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureLifecycle(serviceCollection);

            serviceCollection.AddSingleton(settings);
            return new RavenInstaller(serviceCollection);
        }
    }
}