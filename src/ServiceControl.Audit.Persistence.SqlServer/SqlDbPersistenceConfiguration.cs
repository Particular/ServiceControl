namespace ServiceControl.Audit.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Auditing.BodyStorage;
    using Infrastructure.Settings;
    using UnitOfWork;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            serviceCollection.AddSingleton(sp => new SqlDbConnectionManager(settings.SqlStorageConnectionString));
            serviceCollection.AddSingleton<IAuditDataStore, SqlDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, SqlAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, SqlDbAuditIngestionUnitOfWorkFactory>();
        }
    }
}
