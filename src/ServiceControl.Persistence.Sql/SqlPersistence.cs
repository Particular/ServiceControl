namespace ServiceControl.Persistence.Sql;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.Recoverability;

class SqlPersistence : IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        services.AddSingleton<IServiceControlSubscriptionStorage, NoOpServiceControlSubscriptionStorage>();
        services.AddSingleton<ISubscriptionStorage>(p => p.GetRequiredService<IServiceControlSubscriptionStorage>());

        services.AddSingleton<IBodyStorage, NoOpBodyStorage>();
        services.AddSingleton<IFailedMessageViewIndexNotifications, NoOpFailedMessageViewIndexNotifications>();
        services.AddSingleton<IExternalIntegrationRequestsDataStore, NoOpExternalIntegrationRequestsDataStore>();

        services.AddSingleton<IArchiveMessages, NoOpArchiveMessages>();
        services.AddSingleton<ICustomChecksDataStore, NoOpCustomChecksDataStore>();
        services.AddSingleton<IErrorMessageDataStore, NoOpErrorMessageDataStore>();
        services.AddSingleton<IEventLogDataStore, NoOpEventLogDataStore>();
        services.AddSingleton<IFailedErrorImportDataStore, NoOpFailedErrorImportDataStore>();
        services.AddSingleton<IGroupsDataStore, NoOpGroupsDataStore>();
        services.AddSingleton<IMessageRedirectsDataStore, NoOpMessageRedirectsDataStore>();
        services.AddSingleton<IMonitoringDataStore, NoOpMonitoringDataStore>();
        services.AddSingleton<IQueueAddressStore, NoOpQueueAddressStore>();
        services.AddSingleton<IRetryBatchesDataStore, NoOpRetryBatchesDataStore>();
        services.AddSingleton<IRetryDocumentDataStore, NoOpRetryDocumentDataStore>();
        services.AddSingleton<IRetryHistoryDataStore, NoOpRetryHistoryDataStore>();
        services.AddSingleton<IEndpointSettingsStore, NoOpEndpointSettingsStore>();
        services.AddSingleton<ITrialLicenseDataProvider, NoOpTrialLicenseDataProvider>();
    }

    public void AddInstaller(IServiceCollection services)
    {
    }
}
