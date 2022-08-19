namespace ServiceControl.Audit.Persistence.InMemory
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Infrastructure.Settings;

    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            serviceCollection.AddSingleton<IAuditDataStore, InMemoryAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, InMemoryAttachmentsBodyStorage>();
        }
    }
}
