namespace ServiceControl.Audit.Persistence.SqlServer
{
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using UnitOfWork;

    class SqlDbPersistence : IPersistence
    {
        public SqlDbPersistence(string connectionString) => this.connectionString = connectionString;

        public IPersistenceLifecycle CreateLifecycle(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(sp => new SqlDbConnectionManager(connectionString));
            serviceCollection.AddSingleton<IAuditDataStore, SqlDbAuditDataStore>();
            serviceCollection.AddSingleton<IBodyStorage, SqlAttachmentsBodyStorage>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, SqlDbAuditIngestionUnitOfWorkFactory>();

            return new SqlDbPersistenceLifecycle();
        }

        public IPersistenceInstaller CreateInstaller() => new SqlDbPersistenceInstaller();

        readonly string connectionString;
    }
}