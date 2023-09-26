using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.ServerWide.Operations;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.RavenDb5;
using TestHelper;

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
    static HashSet<int> allPortsInUse = new HashSet<int>();
    List<int> usedPorts = new List<int>();

    public void CustomizeSettings(Settings settings)
    {
        settings.PersisterSpecificSettings = new RavenDBPersisterSettings
        {
            ErrorRetentionPeriod = TimeSpan.FromDays(10),
            ConnectionString = SharedDatabaseSetup.SharedInstance.ServerUrl,
            DatabaseName = DatabaseName
        };
    }

    public int LeasePort()
    {
        lock (allPortsInUse)
        {
            var start = allPortsInUse.Any() ? allPortsInUse.Max() + 1 : 33334;
            var port = PortUtility.FindAvailablePort(start);
            allPortsInUse.Add(port);
            usedPorts.Add(port);
            TestContext.Out.WriteLine($"Port leased: {port}");
            return port;
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (allPortsInUse)
        {
            foreach (var port in usedPorts)
            {
                allPortsInUse.Remove(port);
            }
        }

        var documentStore = await SharedDatabaseSetup.SharedInstance.Connect();

        // Comment this out temporarily to be able to inspect a database after the test has completed
        var deleteDatabasesOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = new[] { DatabaseName }, HardDelete = true });
        await documentStore.Maintenance.Server.SendAsync(deleteDatabasesOperation);
    }
}