namespace ServiceControl.Audit.Persistence.InMemory
{
    using Microsoft.Extensions.DependencyInjection;

    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IAuditDataStore, InMemoryAuditDataStore>();
        }
    }
}
