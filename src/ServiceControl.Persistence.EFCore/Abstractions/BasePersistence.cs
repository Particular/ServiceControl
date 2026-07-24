namespace ServiceControl.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.EFCore.Implementation;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.UnitOfWork;

public abstract class BasePersistence
{
    protected static void RegisterDataStores(IServiceCollection services, EFPersisterSettings settings)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MinimumRequiredStorageState>();

        services.AddSingleton<IServiceControlSubscriptionStorage, SubscriptionStorage>();
        services.AddSingleton<ISubscriptionStorage>(p => p.GetRequiredService<IServiceControlSubscriptionStorage>());

        services.AddUnitOfWorkFactory<EFIngestionUnitOfWorkFactory>();
        services.AddSingleton<IBodyStorage, BodyStorage>();

        services.AddSingleton<FailedMessageViewIndexNotifications>();
        services.AddSingleton<IFailedMessageViewIndexNotifications>(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());
        services.AddHostedService(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());

        services.AddSingleton<ExternalIntegrationRequestsDataStore>();
        services.AddSingleton<IExternalIntegrationRequestsDataStore>(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());
        services.AddHostedService(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());

        services.AddHostedService<RetentionSweeper>();

        services.AddSingleton<IArchiveMessages, MessageArchiver>();
        services.AddSingleton<ICustomChecksDataStore, CustomCheckDataStore>();
        services.AddSingleton<IErrorMessageDataStore, ErrorMessagesDataStore>();
        services.AddSingleton<IEventLogDataStore, EventLogDataStore>();
        services.AddSingleton<IFailedErrorImportDataStore, FailedErrorImportDataStore>();
        services.AddSingleton<IGroupsDataStore, GroupsDataStore>();
        services.AddSingleton<IMessageRedirectsDataStore, MessageRedirectsDataStore>();
        services.AddSingleton<IMonitoringDataStore, MonitoringDataStore>();
        services.AddSingleton<IQueueAddressStore, QueueAddressStore>();
        services.AddSingleton<IRetryBatchesDataStore, RetryBatchesDataStore>();
        services.AddSingleton<IRetryDocumentDataStore, RetryDocumentDataStore>();
        services.AddSingleton<IRetryHistoryDataStore, RetryHistoryDataStore>();
        services.AddSingleton<IEndpointSettingsStore, EndpointSettingsStore>();
        services.AddSingleton<ITrialLicenseDataProvider, TrialLicenseDataProvider>();

        services.AddSingleton<ILicensingDataStore, LicensingDataStore>();

        RegisterBodyStorage(services, settings);
    }

    static void RegisterBodyStorage(IServiceCollection services, EFPersisterSettings settings)
    {
        switch (settings.BodyStorageType)
        {
            case BodyStorageType.FileSystem:
                services.AddSingleton<IBodyStoragePersistence, FileSystemBodyStoragePersistence>();
                break;
            case BodyStorageType.AzureBlob:
                services.AddSingleton<IBodyStoragePersistence, AzureBlobBodyStoragePersistence>();
                break;
            case BodyStorageType.S3:
                throw new NotImplementedException($"{settings.BodyStorageType} body storage is not yet implemented.");
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.BodyStorageType, "Unknown body storage type.");
        }
    }

    // Only stores needing setup-time provisioning register an installer; SetupCommand skips when none is.
    protected static void RegisterBodyStorageInstaller(IServiceCollection services, EFPersisterSettings settings)
    {
        switch (settings.BodyStorageType)
        {
            case BodyStorageType.FileSystem:
                services.AddScoped<IBodyStorageInstaller, FileSystemBodyStorageInstaller>();
                break;
            case BodyStorageType.AzureBlob:
                services.AddScoped<IBodyStorageInstaller, AzureBlobBodyStorageInstaller>();
                break;
            // S3 will register its own installer once implemented.
            case BodyStorageType.S3:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.BodyStorageType, "Unknown body storage type.");
        }
    }
}
