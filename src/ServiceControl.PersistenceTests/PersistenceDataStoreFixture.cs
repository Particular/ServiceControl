namespace ServiceControl.Persistence.Tests
{
    using System.Threading.Tasks;
    using Operations;
    using Persistence;

    abstract class PersistenceDataStoreFixture
    {
        public ICustomChecksDataStore CustomCheckDataStore { get; protected set; }
        public IMonitoringDataStore MonitoringDataStore { get; protected set; }
        public IIngestionUnitOfWorkFactory UnitOfWorkFactory { get; protected set; }

        public abstract Task SetupDataStore();

        public virtual Task CompleteDBOperation() => Task.CompletedTask;

        public virtual Task CleanupDB() => Task.CompletedTask;
    }
}