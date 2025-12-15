namespace ServiceControl.Persistence.Sql.Core.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.UnitOfWork;
using Implementation;
using Implementation.UnitOfWork;

public abstract class BasePersistence
{
    protected static void RegisterDataStores(IServiceCollection services, bool maintenanceMode)
    {
        if (maintenanceMode)
        {
            return;
        }

        services.AddSingleton<MinimumRequiredStorageState>();
        services.AddSingleton<ITrialLicenseDataProvider, TrialLicenseDataProvider>();
        services.AddSingleton<IEndpointSettingsStore, EndpointSettingsStore>();
        services.AddSingleton<IEventLogDataStore, EventLogDataStore>();
        services.AddSingleton<IMessageRedirectsDataStore, MessageRedirectsDataStore>();
        services.AddSingleton<IServiceControlSubscriptionStorage, ServiceControlSubscriptionStorage>();
        services.AddSingleton<IQueueAddressStore, QueueAddressStore>();
        services.AddSingleton<IMonitoringDataStore, MonitoringDataStore>();
        services.AddSingleton<ICustomChecksDataStore, CustomChecksDataStore>();
        services.AddSingleton<Operations.BodyStorage.IBodyStorage, BodyStorage>();
        services.AddSingleton<IRetryHistoryDataStore, RetryHistoryDataStore>();
        services.AddSingleton<IFailedErrorImportDataStore, FailedErrorImportDataStore>();
        services.AddSingleton<IExternalIntegrationRequestsDataStore, ExternalIntegrationRequestsDataStore>();
        services.AddSingleton<IFailedMessageViewIndexNotifications, FailedMessageViewIndexNotifications>();
        services.AddSingleton<Recoverability.IArchiveMessages, ArchiveMessages>();
        services.AddSingleton<IGroupsDataStore, GroupsDataStore>();
        services.AddSingleton<IRetryDocumentDataStore, RetryDocumentDataStore>();
        services.AddSingleton<IRetryBatchesDataStore, RetryBatchesDataStore>();
        services.AddSingleton<INotificationsManager, NotificationsManager>();
        services.AddSingleton<IIngestionUnitOfWorkFactory, IngestionUnitOfWorkFactory>();
        services.AddSingleton<IErrorMessageDataStore, ErrorMessageDataStore>();
    }
}
