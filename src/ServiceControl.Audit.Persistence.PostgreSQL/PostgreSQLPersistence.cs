namespace ServiceControl.Audit.Persistence.PostgreSQL
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Audit.Persistence.PostgreSQL.BodyStorage;
    using ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    public class PostgreSQLPersistence : IPersistence
    {
        public void AddInstaller(IServiceCollection services)
        {
            AddPersistence(services);
        }

        public void AddPersistence(IServiceCollection services)
        {
            services.AddSingleton<IAuditDataStore, PostgreSQLAuditDataStore>();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, PostgreSQLAuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<IFailedAuditStorage, PostgreSQLFailedAuditStorage>();
            services.AddSingleton<IBodyStorage, PostgreSQLAttachmentsBodyStorage>();
            services.AddSingleton<PostgreSQLConnectionFactory>();
        }
    }
}
