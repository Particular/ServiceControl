namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Expiration;
using Raven.Client.Documents.Session;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Persistence.RavenDB.DataMigration;
using ServiceControl.Persistence.Tests;

[TestFixture]
class ReadOnlySourceLifecycleTests
{
    string databaseName;
    IDocumentStore bootstrapStore;
    RavenPersisterSettings sourceSettings;

    [SetUp]
    public async Task SetUp()
    {
        var embeddedServer = await SharedEmbeddedServer.GetInstance();
        databaseName = Guid.NewGuid().ToString("n");

        bootstrapStore = new DocumentStore
        {
            Urls = [embeddedServer.ServerUrl],
            Database = databaseName,
            Conventions = new DocumentConventions { SaveEnumsAsIntegers = true }
        }.Initialize();

        await bootstrapStore.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));
        await bootstrapStore.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord($"{databaseName}-throughput")));

        using (var session = bootstrapStore.OpenAsyncSession())
        {
            await session.StoreAsync(new FailedMessage { UniqueMessageId = "abc", Status = FailedMessageStatus.Archived }, "FailedMessages/abc");
            await session.SaveChangesAsync();
        }

        sourceSettings = new RavenPersisterSettings
        {
            DatabaseName = databaseName,
            ThroughputDatabaseName = $"{databaseName}-throughput",
            ConnectionString = embeddedServer.ServerUrl,
            ErrorRetentionPeriod = TimeSpan.FromDays(10)
        };
    }

    [TearDown]
    public void TearDown() => bootstrapStore?.Dispose();

    [Test]
    public async Task Opening_the_source_creates_no_index()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        var statistics = await bootstrapStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetStatisticsOperation());

        Assert.That(statistics.CountOfIndexes, Is.Zero, "Opening a migration source must not create the fifteen ServiceControl indexes on it.");
    }
    [Test]
    public async Task Opening_the_source_creates_no_database()
    {
        var absentThroughput = $"{databaseName}-absent";
        sourceSettings.ThroughputDatabaseName = absentThroughput;

        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await lifecycle.Open());

        Assert.That(exception.Message, Does.Contain(absentThroughput).And.Contain("LicensingComponent/RavenDB/ThroughputDatabaseName"));
        Assert.That(await bootstrapStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(absentThroughput)), Is.Null, "Opening a migration source must not create a database that was missing.");
    }

    [Test]
    public async Task Opening_the_source_writes_no_database_settings()
    {
        var before = (await bootstrapStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName))).Settings;

        await using (var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings))
        {
            await lifecycle.Open();
        }

        var after = (await bootstrapStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName))).Settings;

        Assert.That(after, Is.EquivalentTo(before), "Opening a migration source must not rewrite the settings of the database it reads.");
    }

    [Test]
    public async Task Opening_the_source_configures_no_expiration()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        var record = await bootstrapStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName));

        Assert.That(record.Expiration, Is.Null, "Opening a migration source must not enable RavenDB document expiry against it, or the customer's fallback data is deleted while they are migrating.");
    }

    [Test]
    public async Task Writing_through_the_source_store_throws()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        using var session = lifecycle.DocumentStore.OpenAsyncSession(new SessionOptions { Database = databaseName });
        await session.StoreAsync(new FailedMessage { UniqueMessageId = "def", Status = FailedMessageStatus.Unresolved }, "FailedMessages/def");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.SaveChangesAsync());

        Assert.That(exception.Message, Does.Contain("open read-only"));
        await AssertAbsent("FailedMessages/def");
    }

    [Test]
    public async Task A_source_session_cannot_even_stage_a_write()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        using var session = lifecycle.OpenSession(databaseName);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.StoreAsync(new FailedMessage { UniqueMessageId = "def", Status = FailedMessageStatus.Unresolved }, "FailedMessages/def"));

        Assert.That(exception.Message, Does.Contain("tracking is disabled"), "OpenSession is NoTracking, so a write through it fails at Store rather than reaching the OnBeforeStore refusal. Both guards have to hold: this one is the only one a copier's own sessions ever meet.");
    }

    [Test]
    public async Task Failed_message_status_reads_back_as_stored()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        using var session = lifecycle.OpenSession(databaseName);
        var loaded = await session.LoadAsync<FailedMessage>("FailedMessages/abc");

        Assert.That(loaded.Status, Is.EqualTo(FailedMessageStatus.Archived), "Without SaveEnumsAsIntegers every message status is misread, and it looks like data corruption rather than a missing convention.");
    }

    [Test]
    public async Task A_failed_open_leaves_no_store_behind()
    {
        sourceSettings.ThroughputDatabaseName = $"{databaseName}-absent";

        var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await lifecycle.Open());

        var afterFailure = Assert.Throws<InvalidOperationException>(() => _ = lifecycle.DocumentStore);

        Assert.That(afterFailure.Message, Does.Contain("is not open"), "A failed Open must dispose what it created. The caller never received a source, so nothing else can, and on the embedded path what leaks is a live RavenDB server process holding the customer's data directory.");

        await lifecycle.DisposeAsync();
    }
    [Test]
    public async Task The_RavenDB_configuration_opens_a_source_that_describes_itself()
    {
        var factory = (IMigrationSourceFactory)new RavenPersistenceConfiguration();

        await using var source = factory.CreateSource(sourceSettings);
        await source.Open();
        var description = await source.Describe();

        Assert.Multiple(() =>
        {
            Assert.That(description.Embedded, Is.False);
            Assert.That(description.PrimaryDatabase, Is.EqualTo(databaseName));
            Assert.That(description.ThroughputDatabase, Is.EqualTo($"{databaseName}-throughput"));
            Assert.That(description.ServerVersion, Does.StartWith("6."));
        });
    }

    [Test]
    public async Task An_unopened_source_refuses_to_describe_itself()
    {
        var factory = (IMigrationSourceFactory)new RavenPersistenceConfiguration();

        await using var source = factory.CreateSource(sourceSettings);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await source.Describe());

        Assert.That(exception.Message, Does.Contain("is not open"), "Separating construction from Open is what lets a host register a source in its container, and it buys a state where nothing is connected yet. That state has to fail loudly rather than return an empty description.");
    }

    [Test]
    public async Task The_source_counts_the_collections_it_finds()
    {
        var factory = (IMigrationSourceFactory)new RavenPersistenceConfiguration();

        await using var source = factory.CreateSource(sourceSettings);
        await source.Open();
        var collections = await source.CountCollections(MigrationSourceDatabase.Primary);

        Assert.That(collections["FailedMessages"], Is.EqualTo(1));
    }

    [Test]
    public async Task Opening_the_source_twice_is_refused()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await lifecycle.Open());

        Assert.That(exception.Message, Does.Contain("already open"), "A second Open would abandon the first store, and on the embedded path a running server process with it.");
    }

    [Test]
    public async Task An_embedded_open_that_cannot_start_leaves_nothing_behind()
    {
        sourceSettings.ConnectionString = null;
        sourceSettings.DatabasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));

        var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);

        Assert.CatchAsync(async () => await lifecycle.Open());

        var afterFailure = Assert.Throws<InvalidOperationException>(() => _ = lifecycle.DocumentStore);

        Assert.That(afterFailure.Message, Does.Contain("is not open"), "A failed embedded open must leave no store, or the caller cannot tell an unopened source from a half-open one.");

        await lifecycle.DisposeAsync();
    }

    [Test]
    public async Task Generating_an_id_is_refused()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        using var session = lifecycle.DocumentStore.OpenAsyncSession(new SessionOptions { Database = databaseName });

        Assert.CatchAsync(async () =>
            await session.StoreAsync(new FailedMessage { UniqueMessageId = "hilo", Status = FailedMessageStatus.Unresolved }));

        Assert.That(await CountDocuments(), Is.EqualTo(1), "HiLo writes an id range to the source before SaveChanges is reached, so a guard that only sees SaveChanges lets it through.");
    }

    [Test]
    public async Task A_patch_against_the_source_is_refused()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        Assert.CatchAsync(async () =>
            await lifecycle.DocumentStore.Operations.ForDatabase(databaseName).SendAsync(
                new PatchOperation("FailedMessages/abc", null, new PatchRequest { Script = "this.Status = 1;" })));

        Assert.That(await LoadStatus("FailedMessages/abc"), Is.EqualTo(FailedMessageStatus.Archived), "A patch never goes through a session, so it bypasses OnBeforeStore entirely. This is the write style the RavenDB persister itself uses.");
    }

    [Test]
    public async Task A_bulk_insert_into_the_source_is_refused()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        Assert.CatchAsync(async () =>
        {
            await using var bulk = lifecycle.DocumentStore.BulkInsert(databaseName);
            await bulk.StoreAsync(new FailedMessage { UniqueMessageId = "bulk", Status = FailedMessageStatus.Unresolved }, "FailedMessages/bulk");
        });

        await AssertAbsent("FailedMessages/bulk");
    }

    [Test]
    public async Task Reconfiguring_expiry_on_the_source_is_refused()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        Assert.CatchAsync(async () =>
            await lifecycle.DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(
                new ConfigureExpirationOperation(new ExpirationConfiguration { Disabled = false, DeleteFrequencyInSec = 60 })));

        var record = await bootstrapStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName));

        Assert.That(record.Expiration, Is.Null, "Enabling expiry on the source would start deleting the customer fallback data.");
    }

    [Test]
    public async Task Deleting_an_untracked_document_is_refused()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        using var session = lifecycle.DocumentStore.OpenAsyncSession(new SessionOptions { Database = databaseName });
        session.Delete("FailedMessages/abc");

        Assert.CatchAsync(async () => await session.SaveChangesAsync());

        Assert.That(await CountDocuments(), Is.EqualTo(1), "Delete by id on an untracked document is deferred, so it never raises OnBeforeDelete.");
    }

    [Test]
    public async Task Reading_the_source_still_works()
    {
        await using var lifecycle = new RavenReadOnlySourceLifecycle(sourceSettings);
        await lifecycle.Open();

        using var session = lifecycle.OpenSession(databaseName);

        var loaded = await session.LoadAsync<FailedMessage>("FailedMessages/abc");
        var queried = await session.Query<FailedMessage>().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null, "A guard that fails closed still has to let every read the migration needs through.");
            Assert.That(queried, Has.Count.EqualTo(1));
        });
    }

    async Task AssertAbsent(string documentId)
    {
        using var session = bootstrapStore.OpenAsyncSession();
        var found = await session.LoadAsync<FailedMessage>(documentId);

        Assert.That(found, Is.Null, $"The source refused the write, so '{documentId}' must not be in the database. Asserting the exception alone tests the guard, not the guarantee.");
    }

    async Task<long> CountDocuments()
    {
        var statistics = await bootstrapStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetStatisticsOperation());
        return statistics.CountOfDocuments;
    }

    async Task<FailedMessageStatus> LoadStatus(string documentId)
    {
        using var session = bootstrapStore.OpenAsyncSession();
        var found = await session.LoadAsync<FailedMessage>(documentId);
        return found.Status;
    }
}
