namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Collections;
    using ServiceBus.Management.Infrastructure.Settings;

    public class PersistenceTestCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new PersistenceDataStoreFixture(DataStoreType.InMemory);
            yield return new PersistenceDataStoreFixture(DataStoreType.RavenDb);

            if (!string.IsNullOrEmpty(SettingsReader<string>.Read("ServiceControl", "SqlStorageConnectionString", "")))
            {
                yield return new PersistenceDataStoreFixture(DataStoreType.SqlDb);
            }
        }
    }
}