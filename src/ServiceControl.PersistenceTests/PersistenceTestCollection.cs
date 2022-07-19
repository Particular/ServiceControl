namespace ServiceControl.Persistence.Tests
{
    using System.Collections;
    using ServiceBus.Management.Infrastructure.Settings;

    public class PersistenceTestCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new PersistenceDataStoreFixture(DataStoreType.InMemory);
            yield return new PersistenceDataStoreFixture(DataStoreType.RavenDb);
            yield return new PersistenceDataStoreFixture(DataStoreType.SqlDb);
        }
    }
}