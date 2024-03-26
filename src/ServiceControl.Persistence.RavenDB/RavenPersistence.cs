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
        public void AddPersistence(IServiceCollection services)
        {
            services.AddSingleton<PersistenceSettings>(settings);

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

        public void AddInstaller(IServiceCollection services)
        {
            ConfigureLifecycle(services);

            // As in intermediate step we are using the hosted service only here to bootstrap the persistence
            // The production code still uses the custom lifetime which eventually we need to address by
            // introducing a document store provider similar to what the audit instance does or synchronously
            // initialize the document store as part of the container wiring.
            services.AddHostedService<RavenPersistenceLifecycleHostedService>();
        }

        void ConfigureLifecycle(IServiceCollection services)
        {
            services.AddSingleton(settings);

            if (settings.UseEmbeddedServer)
            {
                services.AddSingleton<RavenEmbeddedPersistenceLifecycle>();
                // This binding is only necessary as long as we keep around the custom lifetimes
                services.AddSingleton<IPersistenceLifecycle>(b => b.GetService<RavenEmbeddedPersistenceLifecycle>());
                services.AddSingleton<IRavenPersistenceLifecycle>(b => b.GetService<RavenEmbeddedPersistenceLifecycle>());
                services.AddSingleton(b => b.GetService<RavenEmbeddedPersistenceLifecycle>().GetDocumentStore());
                return;
            }

            services.AddSingleton<RavenExternalPersistenceLifecycle>();
            // This binding is only necessary as long as we keep around the custom lifetimes
            services.AddSingleton<IPersistenceLifecycle>(b => b.GetService<RavenExternalPersistenceLifecycle>());
            services.AddSingleton<IRavenPersistenceLifecycle>(b => b.GetService<RavenExternalPersistenceLifecycle>());
            services.AddSingleton(b => b.GetService<RavenExternalPersistenceLifecycle>().GetDocumentStore());
        }
    }
}