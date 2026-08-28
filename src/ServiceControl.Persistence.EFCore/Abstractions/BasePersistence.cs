namespace ServiceControl.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;
using ServiceControl.CustomChecks;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.EFCore.Implementation;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;
using ServiceControl.Persistence.EFCore.Implementation.Recoverability;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.UnitOfWork;
using ServiceControl.Recoverability;

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
        services.AddSingleton<IEnvironmentDataProvider, EFEnvironmentDataProvider>();

        services.AddSingleton<ExternalIntegrationRequestsDataStore>();
        services.AddSingleton<IExternalIntegrationRequestsDataStore>(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());
        services.AddHostedService(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());

        if (settings.RunRetentionSweep)
        {
            services.AddSingleton<RetentionMetrics>();
            services.AddHostedService<RetentionSweeper>();
        }

        services.AddSingleton<OperationsManager>();
        services.AddSingleton<IArchiveMessages, MessageArchiver>();
        services.AddSingleton<ICustomChecksDataStore, CustomCheckDataStore>();
        services.AddSingleton<IMessagesViewDataStore, MessagesViewDataStore>();
        services.AddSingleton<IFailedMessageQueryDataStore, FailedMessageQueryDataStore>();
        services.AddSingleton<IFailedMessageLifecycleDataStore, FailedMessageLifecycleDataStore>();
        services.AddSingleton<IFailedMessageRetryDataStore, FailedMessageRetryDataStore>();
        services.AddSingleton<IEditFailedMessagesDataStore, EditFailedMessagesDataStore>();
        services.AddSingleton<INotificationsDataStore, NotificationsDataStore>();
        services.AddSingleton<IEventLogDataStore, EventLogDataStore>();
        services.AddSingleton<IFailedErrorImportDataStore, FailedErrorImportDataStore>();
        services.AddSingleton<IGroupsDataStore, GroupsDataStore>();
        services.AddSingleton<IMessageRedirectsDataStore, MessageRedirectsDataStore>();
        services.AddSingleton<IMonitoringDataStore, MonitoringDataStore>();
        services.AddSingleton<IQueueAddressStore, QueueAddressStore>();
        services.AddSingleton<IRetryStagingStore, RetryStagingStore>();
        services.AddSingleton<IRetryBatchStore, RetryBatchStore>();
        services.AddSingleton<IRetryHistoryDataStore, RetryHistoryDataStore>();
        services.AddSingleton<IEndpointSettingsStore, EndpointSettingsStore>();
        services.AddSingleton<ITrialLicenseDataProvider, TrialLicenseDataProvider>();

        services.AddSingleton<ILicensingDataStore, LicensingDataStore>();

        RegisterBodyStorage(services, settings);
    }

    // Settings are registered under their concrete type so each store resolves only what it can act on.
    static void RegisterBodyStorage(IServiceCollection services, EFPersisterSettings settings)
    {
        services.AddSingleton(settings.BodyStorage);

        switch (settings.BodyStorage)
        {
            case FileSystemBodyStorageSettings fileSystem:
                services.TryAddSingleton(fileSystem);
                services.AddSingleton<IBodyStoragePersistence, FileSystemBodyStoragePersistence>();
                services.AddSingleton<IDriveSpaceProvider, DriveInfoSpaceProvider>();
                services.AddCustomCheck<FileSystemBodyStorageCustomCheck>();
                break;
            case AzureBlobBodyStorageSettings azureBlob:
                services.TryAddSingleton(azureBlob);
                services.AddSingleton<IBodyStoragePersistence, AzureBlobBodyStoragePersistence>();
                break;
            case S3BodyStorageSettings s3:
                services.TryAddSingleton(s3);
                services.AddSingleton<IBodyStoragePersistence, S3BodyStoragePersistence>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.BodyStorage, "Unknown body storage type.");
        }
    }

    // Only stores needing setup-time provisioning register an installer; SetupCommand skips when none is.
    protected static void RegisterBodyStorageInstaller(IServiceCollection services, EFPersisterSettings settings)
    {
        switch (settings.BodyStorage)
        {
            case FileSystemBodyStorageSettings fileSystem:
                services.TryAddSingleton(fileSystem);
                services.AddScoped<IBodyStorageInstaller, FileSystemBodyStorageInstaller>();
                break;
            case AzureBlobBodyStorageSettings azureBlob:
                services.TryAddSingleton(azureBlob);
                services.AddScoped<IBodyStorageInstaller, AzureBlobBodyStorageInstaller>();
                break;
            case S3BodyStorageSettings s3:
                services.TryAddSingleton(s3);
                services.AddScoped<IBodyStorageInstaller, S3BodyStorageInstaller>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.BodyStorage, "Unknown body storage type.");
        }
    }
}
