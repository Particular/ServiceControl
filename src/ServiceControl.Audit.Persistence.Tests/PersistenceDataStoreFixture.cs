namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;

    abstract class PersistenceDataStoreFixture
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public abstract Task SetupDataStore();

        public virtual Task CompleteDBOperation() => Task.CompletedTask;

        public virtual Task CleanupDB() => Task.CompletedTask;
    }
}