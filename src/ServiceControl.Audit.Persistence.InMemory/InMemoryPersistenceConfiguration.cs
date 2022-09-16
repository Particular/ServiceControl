namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, IDictionary<string, string> settings, bool maintenanceMode, bool isSetup)
        {
            serviceCollection.AddSingleton<InMemoryAuditDataStore>();
            serviceCollection.AddSingleton<IAuditDataStore>(sp => sp.GetRequiredService<InMemoryAuditDataStore>());
            serviceCollection.AddSingleton<IBodyStorage, InMemoryAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IFailedAuditStorage, InMemoryFailedAuditStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, InMemoryAuditIngestionUnitOfWorkFactory>();
        }
    }
}
