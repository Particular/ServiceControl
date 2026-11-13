namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using ServiceControl.RavenDB;

// A new database on the shared embedded server with no indexes and, unless a fixture adds them, no settings.
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

        var store = new DocumentStore
        {
            Urls = [embeddedDatabase.ServerUrl],
            Database = databaseName,
            Conventions = DocumentStoreConventions.Create()
        };

        try
        {
            store.Initialize();

            var databaseRecord = new DatabaseRecord(databaseName);
            ConfigureDatabase(databaseRecord);
            await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(databaseRecord), TestTimeoutCancellationToken);
        }
#pragma warning disable PS0019 // The catch rethrows everything, cancellation included
        catch
        {
            store.Dispose();
            throw;
        }
#pragma warning restore PS0019

        Store = store;
    }

    protected virtual void ConfigureDatabase(DatabaseRecord databaseRecord)
    {
    }

    [TearDown]
    public async Task DeleteCleanDatabase()
    {
        try
        {
            if (Store != null)
            {
                await Store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(
                    new DeleteDatabasesOperation.Parameters { DatabaseNames = [databaseName], HardDelete = true }));
            }
        }
        finally
        {
            Store?.Dispose();
            Store = null;
            testCancellationTokenSource?.Dispose();
        }
    }

    string databaseName;
    CancellationTokenSource testCancellationTokenSource;

    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);
}
