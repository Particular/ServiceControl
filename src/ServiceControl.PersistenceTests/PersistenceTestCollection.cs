namespace ServiceControl.Persistence.Tests
{
    using System.Collections;
    using PersistenceTests;
    using ServiceBus.Management.Infrastructure.Settings;

    public class PersistenceTestCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new InMemory();
            yield return new RavenDb();

            var sqlConnectionString = SettingsReader<string>.Read("ServiceControl", "SqlStorageConnectionString", "");

            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                yield return new SqlDb(sqlConnectionString);
            }
        }
    }
}