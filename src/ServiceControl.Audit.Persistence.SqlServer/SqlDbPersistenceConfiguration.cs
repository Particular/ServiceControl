namespace ServiceControl.Audit.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Infrastructure.Settings;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, Settings settings, bool maintenanceMode, bool isSetup)
        {
            serviceCollection.AddSingleton(sp => new SqlDbConnectionManager(settings.SqlStorageConnectionString));
            serviceCollection.AddSingleton<IAuditDataStore, SqlDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, SqlAttachmentsBodyStorage>();
        }
    }
}
