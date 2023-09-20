using System;
using System.IO;
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
        var settings = new RavenDBPersisterSettings
        {
            DatabasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "Embedded"),
            LogPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        };

        using (var embeddedDatabase = EmbeddedDatabase.Start(settings))
        {

            using (var documentStore = await embeddedDatabase.Connect(CancellationToken.None))
            {
                var store = new EventLogDataStore(documentStore);

                _ = await store.GetEventLogItems(new PagingInfo());
            }
        }
    }
}