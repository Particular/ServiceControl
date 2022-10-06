namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using Persistence.UnitOfWork;

    class SessionBasedRavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public SessionBasedRavenDbAuditIngestionUnitOfWorkFactory(
            IRavenDbSessionProvider sessionProvider,
            DatabaseConfiguration databaseConfiguration)
        {
            this.sessionProvider = sessionProvider;
            this.databaseConfiguration = databaseConfiguration;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
            => new SessionBasedRavenDbAuditIngestionUnitOfWork(
                sessionProvider.OpenSession(),
                databaseConfiguration.AuditRetentionPeriod,
                databaseConfiguration.MaxBodySizeToStore);

        readonly IRavenDbSessionProvider sessionProvider;
        readonly DatabaseConfiguration databaseConfiguration;
    }
}