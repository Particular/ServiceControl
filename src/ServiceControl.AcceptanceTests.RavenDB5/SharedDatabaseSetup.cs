using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.ServerWide.Operations;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.RavenDb5;

[SetUpFixture]
public static class SharedDatabaseSetup
{
    public static EmbeddedDatabase SharedInstance { get; private set; }

    // Needs to be in a SetUpFixture otherwise the OneTimeSetUp is invoked for each inherited test fixture
    [OneTimeSetUp]
    public static async Task SetupSharedEmbeddedServer()
    {
        using (var cancellation = new CancellationTokenSource(30_000))
        {
            SharedInstance = await SharedEmbeddedServer.GetInstance(cancellation.Token);
        }
    }

    [OneTimeTearDown]
    public static void TearDown() => SharedInstance.Dispose();

    public static DatabaseLease LeaseDatabase()
    {
        return new DatabaseLease();
    }
}

public class DatabaseLease : IAsyncDisposable
{
    public string DatabaseName { get; } = Guid.NewGuid().ToString("n");

    public void CustomizeSettings(Settings settings)
    {
        settings.PersisterSpecificSettings = new RavenDBPersisterSettings
        {
            ErrorRetentionPeriod = TimeSpan.FromDays(10),
            ConnectionString = SharedDatabaseSetup.SharedInstance.ServerUrl,
            DatabaseName = DatabaseName
        };
    }

    public async ValueTask DisposeAsync()
    {
        var documentStore = await SharedDatabaseSetup.SharedInstance.Connect();

        // Comment this out temporarily to be able to inspect a database after the test has completed
        var deleteDatabasesOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = new[] { DatabaseName }, HardDelete = true });
        await documentStore.Maintenance.Server.SendAsync(deleteDatabasesOperation);
    }
}