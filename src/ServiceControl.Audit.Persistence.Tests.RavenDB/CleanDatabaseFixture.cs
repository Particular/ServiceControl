namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

// A new database on the shared embedded server with no settings and no indexes, as opposed to the one DatabaseSetup prepares.
abstract class CleanDatabaseFixture
{
    protected IDocumentStore Store { get; private set; }

    protected CancellationToken TestTimeoutCancellationToken => testCancellationTokenSource.Token;

    [SetUp]
    public async Task CreateCleanDatabase()
    {
        testCancellationTokenSource = new CancellationTokenSource(TestTimeout);

        var embeddedDatabase = await SharedEmbeddedServer.GetInstance(TestContext.CurrentContext.CancellationToken);
        databaseName = Guid.NewGuid().ToString();

        Store = new DocumentStore
        {
            Urls = [embeddedDatabase.ServerUrl],
            Database = databaseName,
            Conventions = new DocumentConventions { SaveEnumsAsIntegers = true, SaveEnumsAsIntegersForPatching = true }
        };
        Store.Initialize();

        await Store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseName)), TestTimeoutCancellationToken);
    }

    [TearDown]
    public async Task DeleteCleanDatabase()
    {
        if (Store != null)
        {
            await Store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(
                new DeleteDatabasesOperation.Parameters { DatabaseNames = [databaseName], HardDelete = true }));
            Store.Dispose();
        }

        testCancellationTokenSource?.Dispose();
    }

    string databaseName;
    CancellationTokenSource testCancellationTokenSource;

    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);
}
