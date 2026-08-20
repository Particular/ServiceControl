namespace TestHelper;

using System;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.ServerWide.Operations;
using Raven.Embedded;
using ServiceControl.RavenDB;

public static class EmbeddedDatabaseExtensions
{
    public static async Task DeleteDatabase(this EmbeddedDatabase database, string dbName, CancellationToken cancellationToken = default)
    {
        using var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(
            new DatabaseOptions(dbName) { SkipCreatingDatabase = true },
            cancellationToken);

        // RavenDB's default confirmation wait (15s) is for its Raft commit log to catch up
        // with this delete command's index. Under the volume this shared embedded server sees
        // in a full test run (every test creates and deletes its own database against the same
        // instance), that commit can occasionally land just past the default window even though
        // the delete itself isn't failing - so give it more headroom explicitly rather than
        // relying on RavenDB's default.
        await store.Maintenance.Server.SendAsync(
            new DeleteDatabasesOperation(dbName, hardDelete: true, timeToWaitForConfirmation: TimeSpan.FromSeconds(30)),
            cancellationToken);
    }
}
