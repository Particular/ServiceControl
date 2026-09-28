namespace ServiceControl.Audit.Persistence.EFCore.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NServiceBus;
using ServiceControl.Audit.Persistence.EFCore.Implementation;
using ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Audit.Persistence.UnitOfWork;

abstract class EFPersistenceBase(EFPersisterSettings settings) : IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        AddCommon(services);
        AddQueryServices(services);

        services.AddSingleton<AuditDataStore>();
        services.AddSingleton<IAuditMessagesViewDataStore>(provider => provider.GetRequiredService<AuditDataStore>());
        services.AddSingleton<ISagaHistoryDataStore>(provider => provider.GetRequiredService<AuditDataStore>());
        services.AddSingleton<IAuditIngestionUnitOfWorkFactory, AuditIngestionUnitOfWorkFactory>();
        services.AddSingleton<IFailedAuditStorage, FailedAuditStorage>();

        services.AddHostedService<AuditRetention>();

        if (services.SingleOrDefault(s => s.ServiceType == typeof(EndpointConfiguration)) is
            {
                ImplementationInstance: EndpointConfiguration endpointConfiguration
            })
        {
            AddCustomChecks(endpointConfiguration);
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

    protected abstract void AddDbContext(IServiceCollection services, EFPersisterSettings settings);

    protected abstract void AddPartitionManager(IServiceCollection services);

    protected abstract void AddQueryServices(IServiceCollection services);

    protected virtual void AddCustomChecks(EndpointConfiguration endpointConfiguration)
    {
    }
}
