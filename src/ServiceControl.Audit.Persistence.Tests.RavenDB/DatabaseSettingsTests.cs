namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.RavenDB;
using Raven.Client.Documents.Indexes;
using Raven.Client.ServerWide.Operations;
using Raven.Client.ServerWide.Operations.Configuration;

[TestFixture]
class DatabaseSettingsTests : PersistenceTestFixture
{
    [Test]
    public async Task New_database_should_use_lucene()
    {
        var record = await configuration.DocumentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(configuration.DocumentStore.Database), TestTimeoutCancellationToken);

        Assert.That(record.Settings, Does.ContainKey("Indexing.Auto.SearchEngineType").WithValue(nameof(SearchEngineType.Lucene)));
        Assert.That(record.Settings, Does.ContainKey("Indexing.Static.SearchEngineType").WithValue(nameof(SearchEngineType.Lucene)));
    }

    [Test]
    public async Task Setup_should_not_add_search_engine_settings_to_an_existing_database()
    {
        var store = configuration.DocumentStore;
        var databaseName = store.Database;

        // RavenDB Studio removes a database setting whose value equals the server default. Versions 6.20.0 to 6.21.2
        // then added Corax back at every start, also in maintenance mode, and undid the operator's choice.
        await store.Maintenance.ForDatabase(databaseName).SendAsync(new PutDatabaseSettingsOperation(databaseName, []), TestTimeoutCancellationToken);
        await store.Maintenance.Server.SendAsync(new ToggleDatabasesStateOperation(databaseName, true), TestTimeoutCancellationToken);
        await store.Maintenance.Server.SendAsync(new ToggleDatabasesStateOperation(databaseName, false), TestTimeoutCancellationToken);

        var databaseConfiguration = new DatabaseConfiguration(databaseName, 60, true, TimeSpan.FromDays(1), 100_000, 20, 20, new ServerConfiguration(store.Urls[0]), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        await new DatabaseSetup(databaseConfiguration).Execute(store, TestTimeoutCancellationToken);

        var record = await store.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), TestTimeoutCancellationToken);

        Assert.That(record.Settings, Does.Not.ContainKey("Indexing.Auto.SearchEngineType"));
        Assert.That(record.Settings, Does.Not.ContainKey("Indexing.Static.SearchEngineType"));
    }
}
