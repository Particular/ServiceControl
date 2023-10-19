namespace ServiceControl.Persistence.RavenDb
{
    using MessageRedirects;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Persistence.Recoverability;
    using RavenDb5;
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

    class RavenDbPersistence : IPersistence
    {
        public RavenDbPersistence(RavenDBPersisterSettings settings)
        {
            this.settings = settings;
        }

        public void Configure(IServiceCollection serviceCollection)
        {
            if (settings.MaintenanceMode)
            {
                return;
            }

            serviceCollection.AddSingleton<PersistenceSettings>(settings);
            serviceCollection.AddSingleton(settings);

            serviceCollection.AddSingleton<IServiceControlSubscriptionStorage, RavenDbSubscriptionStorage>();
            serviceCollection.AddSingleton<ISubscriptionStorage>(p => p.GetRequiredService<IServiceControlSubscriptionStorage>());

            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddUnitOfWorkFactory<RavenDbIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<ExpirationManager>();
            serviceCollection.AddSingleton<MinimumRequiredStorageState>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();

            serviceCollection.AddSingleton<FailedMessageViewIndexNotifications>();
            serviceCollection.AddSingleton<IFailedMessageViewIndexNotifications>(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());
            serviceCollection.AddHostedService(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());

            serviceCollection.AddSingleton<ExternalIntegrationRequestsDataStore>();
            serviceCollection.AddSingleton<IExternalIntegrationRequestsDataStore>(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());
            serviceCollection.AddHostedService(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());

            serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
            serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();
            serviceCollection.AddCustomCheck<CheckFreeDiskSpace>();
            serviceCollection.AddCustomCheck<CheckMinimumStorageRequiredForIngestion>();

            serviceCollection.AddSingleton<OperationsManager>();

            serviceCollection.AddSingleton<IArchiveMessages, MessageArchiver>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddSingleton<IErrorMessageDataStore, ErrorMessagesDataStore>();
            serviceCollection.AddSingleton<IEventLogDataStore, EventLogDataStore>();
            serviceCollection.AddSingleton<IFailedErrorImportDataStore, FailedErrorImportDataStore>();
            serviceCollection.AddSingleton<IGroupsDataStore, GroupsDataStore>();
            serviceCollection.AddSingleton<IGroupsDataStore, GroupsDataStore>();
            serviceCollection.AddSingleton<IMessageRedirectsDataStore, MessageRedirectsDataStore>();
            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<IQueueAddressStore, QueueAddressStore>();
            serviceCollection.AddSingleton<IRetryBatchesDataStore, RetryBatchesDataStore>();
            serviceCollection.AddSingleton<IRetryDocumentDataStore, RetryDocumentDataStore>();
            serviceCollection.AddSingleton<IRetryHistoryDataStore, RetryHistoryDataStore>();
            serviceCollection.AddSingleton<IServiceControlSubscriptionStorage, RavenDbSubscriptionStorage>();

            // Forward saga audit messages and warn in ServiceControl 5, remove in 6
            serviceCollection.AddSingleton<ISagaAuditDataStore, SagaAuditDeprecationDataStore>();
            serviceCollection.AddCustomCheck<SagaAuditDestinationCustomCheck>();
            serviceCollection.AddSingleton<SagaAuditDestinationCustomCheck.State>();
        }

        public void ConfigureLifecycle(IServiceCollection serviceCollection)
        {
            if (settings.UseEmbeddedServer)
            {
                serviceCollection.AddSingleton<RavenDbEmbeddedPersistenceLifecycle>();
                serviceCollection.AddSingleton<IPersistenceLifecycle>(b => b.GetService<RavenDbEmbeddedPersistenceLifecycle>());
                serviceCollection.AddSingleton(b => b.GetService<RavenDbEmbeddedPersistenceLifecycle>().GetDocumentStore());
                return;
            }


            serviceCollection.AddSingleton<RavenDbExternalPersistenceLifecycle>();
            serviceCollection.AddSingleton<IPersistenceLifecycle>(b => b.GetService<RavenDbExternalPersistenceLifecycle>());
            serviceCollection.AddSingleton(b => b.GetService<RavenDbExternalPersistenceLifecycle>().GetDocumentStore());
        }

        public IPersistenceInstaller CreateInstaller()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureLifecycle(serviceCollection);

            serviceCollection.AddSingleton(settings);
            return new RavenDbInstaller(serviceCollection);
        }

        readonly RavenDBPersisterSettings settings;
    }
}