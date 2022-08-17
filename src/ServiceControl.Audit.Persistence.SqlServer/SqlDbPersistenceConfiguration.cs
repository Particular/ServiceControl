namespace ServiceControl.Audit.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(sp =>
                new SqlDbConnectionManager(sp.GetRequiredService<Settings>().SqlStorageConnectionString));
            serviceCollection.AddSingleton<IAuditDataStore, SqlDbAuditDataStore>();
        }
    }
}
