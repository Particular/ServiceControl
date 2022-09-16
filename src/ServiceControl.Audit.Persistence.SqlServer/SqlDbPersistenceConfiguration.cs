namespace ServiceControl.Audit.Persistence.SqlServer
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using Auditing.BodyStorage;
    using UnitOfWork;
    using System.Collections.Generic;

    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection, IDictionary<string, string> settings, bool maintenanceMode, bool isSetup)
        {
            //TODO: make this safer
            var connectionString = settings["SqlStorageConnectionString"];

            serviceCollection.AddSingleton(sp => new SqlDbConnectionManager(connectionString));
            serviceCollection.AddSingleton<IAuditDataStore, SqlDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, SqlAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, SqlDbAuditIngestionUnitOfWorkFactory>();
        }
    }
}
