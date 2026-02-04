namespace ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

using Azure.Storage.Blobs;
using Implementation;
using Implementation.UnitOfWork;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Auditing.BodyStorage;
using ServiceControl.Audit.Persistence.UnitOfWork;

public abstract class BaseAuditPersistence
{
    protected static void RegisterDataStores(IServiceCollection services, AuditSqlPersisterSettings settings)
    {
        services.AddSingleton<MinimumRequiredStorageState>();
        if (!string.IsNullOrEmpty(settings.MessageBodyStoragePath))
        {
            services.AddSingleton<IBodyStoragePersistence, FileSystemBodyStoragePersistence>();
        }
        else
        {
            var blobClient = new BlobServiceClient(settings.MessageBodyStorageConnectionString);
            var blobContainerClient = blobClient.GetBlobContainerClient("audit-bodies");
            services.AddSingleton<IBodyStoragePersistence>(new AzureBlobBodyStoragePersistence(blobContainerClient, settings));
        }
        services.AddSingleton<IBodyStorage, BodyStorageFetcher>();
        services.AddSingleton<IAuditDataStore, EFAuditDataStore>();
        services.AddSingleton<IFailedAuditStorage, EFFailedAuditStorage>();
        services.AddSingleton<IAuditIngestionUnitOfWorkFactory, AuditIngestionUnitOfWorkFactory>();
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<RetentionCleaner>();
    }
}
