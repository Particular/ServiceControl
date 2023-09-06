namespace ServiceControl.Persistence.RavenDb
{
    using MessageRedirects;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.Recoverability;
    using Recoverability;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;
    using ServiceControl.Persistence.MessageRedirects;
    using ServiceControl.Persistence.RavenDb.SagaAudit;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.Recoverability;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Embedded;

    class RavenDbPersistence : IPersistence
    {
        public RavenDbPersistence(RavenDBPersisterSettings settings, EmbeddableDocumentStore documentStore, RavenStartup ravenStartup)
        {
            this.settings = settings;
            this.documentStore = documentStore;
            this.ravenStartup = ravenStartup;
        }

        public void Configure(IServiceCollection serviceCollection)
        {
            if (settings.MaintenanceMode)
            {
                return;
            }

            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            serviceCollection.AddSingleton<IServiceControlSubscriptionStorage, RavenDbSubscriptionStorage>();
            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddUnitOfWorkFactory<RavenDbIngestionUnitOfWorkFactory>();
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
            serviceCollection.AddCustomCheck<AuditRetentionCustomCheck>();
            serviceCollection.AddCustomCheck<CheckFreeDiskSpace>();
            serviceCollection.AddCustomCheck<FailedAuditImportCustomCheck>();
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
            serviceCollection.AddSingleton<IReclassifyFailedMessages, FailedMessageReclassifier>();
            serviceCollection.AddSingleton<IRetryBatchesDataStore, RetryBatchesDataStore>();
            serviceCollection.AddSingleton<IRetryDocumentDataStore, RetryDocumentDataStore>();
            serviceCollection.AddSingleton<IRetryHistoryDataStore, RetryHistoryDataStore>();
            serviceCollection.AddSingleton<ISagaAuditDataStore, SagaAuditDataStore>();
            serviceCollection.AddSingleton<IServiceControlSubscriptionStorage, RavenDbSubscriptionStorage>();
        }

        public IPersistenceLifecycle CreateLifecycle()
        {
            return new RavenDbPersistenceLifecycle(ravenStartup, documentStore);
        }

        public IPersistenceInstaller CreateInstaller()
        {
            return new RavenDbInstaller(documentStore, ravenStartup);
        }

        readonly RavenStartup ravenStartup;
        readonly RavenDBPersisterSettings settings;
        readonly EmbeddableDocumentStore documentStore;
    }
}
