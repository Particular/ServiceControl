namespace ServiceControl.AcceptanceTests;

using System;
using System.Threading.Tasks;
using Raven.Client.ServerWide.Operations;
using ServiceBus.Management.Infrastructure.Settings;

public class DatabaseLease : IAsyncDisposable
{
    public string DatabaseName { get; } = Guid.NewGuid().ToString("n");

    public void CustomizeSettings(Settings settings)
    {
        settings.PersisterSpecificSettings = new RavenPersisterSettings
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