namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections;
    using ServiceControl.Audit.Infrastructure.Settings;

    public class PersistenceTestCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new InMemory();
            yield return new RavenDb();

            var sqlConnectionString = SettingsReader<string>.Read("ServiceControl.Audit", "SqlStorageConnectionString", "");

            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                yield return new SqlDb(sqlConnectionString);
            }
        }
    }
}