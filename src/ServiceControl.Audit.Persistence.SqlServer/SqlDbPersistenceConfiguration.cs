namespace ServiceControl.Audit.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Auditing.BodyStorage;
    using UnitOfWork;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var connectionString = settings.PersisterSpecificSettings["Sql/ConnectionString"];

            serviceCollection.AddSingleton(sp => new SqlDbConnectionManager(connectionString));
            serviceCollection.AddSingleton<IAuditDataStore, SqlDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, SqlAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, SqlDbAuditIngestionUnitOfWorkFactory>();
        }
    }
}
