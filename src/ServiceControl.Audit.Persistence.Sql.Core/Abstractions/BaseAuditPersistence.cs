namespace ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

using Implementation;
using Implementation.UnitOfWork;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Auditing.BodyStorage;
using ServiceControl.Audit.Persistence.UnitOfWork;

public abstract class BaseAuditPersistence
{
    protected static void RegisterDataStores(IServiceCollection services)
    {
        services.AddSingleton<MinimumRequiredStorageState>();
        services.AddSingleton<FileSystemBodyStorageHelper>();
        services.AddSingleton<IBodyStorage, EFBodyStorage>();
        services.AddSingleton<IAuditDataStore, EFAuditDataStore>();
        services.AddSingleton<IFailedAuditStorage, EFFailedAuditStorage>();
        services.AddSingleton<IAuditIngestionUnitOfWorkFactory, AuditIngestionUnitOfWorkFactory>();
        services.AddHostedService<RetentionCleaner>();
    }
}
