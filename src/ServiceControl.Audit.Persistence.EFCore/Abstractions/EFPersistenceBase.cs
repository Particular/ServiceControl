namespace ServiceControl.Audit.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NServiceBus;
using ServiceControl.Persistence.BodyStorage;
using ServiceControl.Audit.Persistence.EFCore.Implementation;
using ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Audit.Persistence.UnitOfWork;

abstract class EFPersistenceBase(EFPersisterSettings settings) : IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        AddCommon(services);
        AddQueryServices(services);
        AddBodyStorage(services);

        services.AddSingleton<AuditDataStore>();
        services.AddSingleton<IAuditMessagesViewDataStore>(provider => provider.GetRequiredService<AuditDataStore>());
        services.AddSingleton<ISagaHistoryDataStore>(provider => provider.GetRequiredService<AuditDataStore>());
        services.AddSingleton<IAuditIngestionUnitOfWorkFactory, AuditIngestionUnitOfWorkFactory>();
        services.AddSingleton<IFailedAuditStorage, FailedAuditStorage>();

        services.AddSingleton<AuditRetentionCustomCheck.State>();
        services.AddHostedService<AuditRetention>();

        if (services.SingleOrDefault(s => s.ServiceType == typeof(EndpointConfiguration)) is
            {
                ImplementationInstance: EndpointConfiguration endpointConfiguration
            })
        {
            endpointConfiguration.AddCustomCheck<AuditRetentionCustomCheck>();
            AddCustomChecks(endpointConfiguration);

            if (settings.BodyStorage is FileSystemBodyStorageSettings)
            {
                endpointConfiguration.AddCustomCheck<FileSystemBodyStorageCustomCheck>();
            }
        }
    }

    public void AddInstaller(IServiceCollection services)
    {
        AddCommon(services);
        services.AddHostedService<DatabaseSetup>();
    }

    void AddCommon(IServiceCollection services)
    {
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        AddDbContext(services, settings);
        AddPartitionManager(services);
    }

    // The settings type picks the store, so each store resolves only the settings it can act on.
    void AddBodyStorage(IServiceCollection services)
    {
        switch (settings.BodyStorage)
        {
            case FileSystemBodyStorageSettings fileSystem:
                services.AddSingleton(fileSystem);
                services.AddSingleton<IBodyStoragePersistence, FileSystemBodyStoragePersistence>();
                services.AddSingleton<IDriveSpaceProvider, DriveInfoSpaceProvider>();
                break;
            case AzureBlobBodyStorageSettings azureBlob:
                services.AddSingleton(azureBlob);
                services.AddSingleton<IBodyStoragePersistence, AzureBlobBodyStoragePersistence>();
                break;
            case S3BodyStorageSettings s3:
                services.AddSingleton(s3);
                services.AddSingleton<IBodyStoragePersistence, S3BodyStoragePersistence>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.BodyStorage, "Unknown body storage type.");
        }
    }

    protected abstract void AddDbContext(IServiceCollection services, EFPersisterSettings settings);

    protected abstract void AddPartitionManager(IServiceCollection services);

    protected abstract void AddQueryServices(IServiceCollection services);

    protected virtual void AddCustomChecks(EndpointConfiguration endpointConfiguration)
    {
    }
}
