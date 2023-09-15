using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.RavenDb;
using ServiceControl.Persistence.RavenDb5;

[TestFixture]
class EmbeddedDatabaseTests
{
    [Test]
    public async Task CanStart()
    {
        using (var embeddedDatabase = EmbeddedDatabase.Start(new RavenDBPersisterSettings { }))
        {

            using (var documentStore = await embeddedDatabase.Connect(CancellationToken.None))
            {
                var store = new EventLogDataStore(documentStore);

                _ = await store.GetEventLogItems(new PagingInfo());
            }
        }
    }
}