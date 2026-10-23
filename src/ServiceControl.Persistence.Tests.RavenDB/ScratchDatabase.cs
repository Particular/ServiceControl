namespace ServiceControl.Persistence.Tests.RavenDB;

using System;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using ServiceControl.RavenDB;
using TestHelper;

// An empty database on the shared embedded server, created with the search engine settings that DatabaseSetup uses for new databases.
sealed class ScratchDatabase : IAsyncDisposable
{
    ScratchDatabase(EmbeddedDatabase server, string name, DocumentStore store)
    {
        this.server = server;
        this.name = name;
        Store = store;
    }

    public IDocumentStore Store { get; }

    public static async Task<ScratchDatabase> Create(CancellationToken cancellationToken = default)
    {
        var server = await SharedEmbeddedServer.GetInstance(cancellationToken);
        var name = Guid.NewGuid().ToString("n");

        var store = new DocumentStore
        {
            Urls = [server.ServerUrl],
            Database = name,
            Conventions = new DocumentConventions { SaveEnumsAsIntegers = true }
        };
        store.Initialize();

        var databaseRecord = new DatabaseRecord(name);
        databaseRecord.Settings.Add("Indexing.Auto.SearchEngineType", "Lucene");
        databaseRecord.Settings.Add("Indexing.Static.SearchEngineType", "Lucene");
        await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(databaseRecord), cancellationToken);

        return new ScratchDatabase(server, name, store);
    }

    public async ValueTask DisposeAsync()
    {
        Store.Dispose();
        await server.DeleteDatabase(name);
    }

    readonly EmbeddedDatabase server;
    readonly string name;
}
