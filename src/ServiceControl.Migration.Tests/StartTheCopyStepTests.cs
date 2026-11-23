namespace ServiceControl.Migration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Unicast.Subscriptions;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Contracts.Operations;
using ServiceControl.Hosting.Commands;
using ServiceControl.MessageFailures;
using ServiceControl.Migration.Guided;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

[TestFixture(MigrationTestTarget.SqlServer)]
[TestFixture(MigrationTestTarget.PostgreSql)]
[NonParallelizable]
class StartTheCopyStepTests(string persisterType)
{
    const string SubscriptionsDocumentId = "Subscriptions/All";

    Settings settings;
    string connectionString;
    string schema;
    string bodyStoragePath;
    string fixtureBodyStoragePath;
    readonly List<string> variables = [];
    readonly HashSet<string> seededDocumentIds = [];
    readonly HashSet<string> seededThroughputDocumentIds = [];

    [OneTimeSetUp]
    public async Task CreateSchema()
    {
        schema = MigrationTestTarget.NewSchema();
        connectionString = await MigrationTestTarget.ConnectionString(persisterType);

        try
        {
            SetVariables();
            fixtureBodyStoragePath = bodyStoragePath;

            await MigrationTestTarget.ApplySchema(settings);
        }
        finally
        {
            ClearVariables();
        }
    }

    [OneTimeTearDown]
    public async Task RemoveSchema()
    {
        try
        {
            await MigrationTestTarget.DropSchema(persisterType, schema);
        }
        finally
        {
            DeleteBodyStorage(fixtureBodyStoragePath);
        }
    }

    [SetUp]
    public void SetUp() => SetVariables();

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            await DeleteSeededDocuments();
        }
        finally
        {
            try
            {
                DeleteBodyStorage(bodyStoragePath);
            }
            finally
            {
                ClearVariables();
            }
        }
    }

    [Test]
    public async Task Answering_no_leaves_RavenDB_body_storage_and_the_target_untouched_and_the_next_run_asks_again()
    {
        // A row the copy would store in each category these helpers can seed, so a run that wrote anything leaves it behind.
        await SeedStorableFailedMessageWithBody();
        await SeedSubscription("MyApp.OrderPlaced", majorVersion: 1, "sales@transport", "Sales");
        await Seed("GroupComment/answer-no-writes-nothing", new GroupComment { Id = "GroupComment/answer-no-writes-nothing", Comment = "Investigating" });
        await Seed("FailedErrorImports/answer-no-writes-nothing", new FailedErrorImport { Id = "FailedErrorImports/answer-no-writes-nothing", ExceptionInfo = "Deserialization failed" });
        await SeedThroughputEndpoint("Billing", global::Particular.LicensingComponent.Contracts.ThroughputSource.Broker);

        using var sourceStore = OpenSourceStore();

        // The database etag also moves when a document, counter or time series changes, which the count alone misses.
        async Task<(long Documents, long? LastDatabaseEtag)> Snapshot(string database)
        {
            var statistics = await sourceStore.Maintenance.ForDatabase(database).SendAsync(new GetStatisticsOperation());
            return (statistics.CountOfDocuments, statistics.LastDatabaseEtag);
        }

        var before = (Primary: await Snapshot(MigrationSourceServer.PrimaryDatabase), Throughput: await Snapshot(MigrationSourceServer.ThroughputDatabase));
        var targetBefore = await TargetState();

        var (exitCode, output) = await RunAnsweringNoToStartTheCopy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output, Does.Contain("Nothing has been written."));
            Assert.That(output, Does.Match(@"(?m)^   UnresolvedAndRetryIssuedFailedMessages\s+[1-9][\d,]*\r?$"), "the rows step counted the seeded failed message before the question");
            Assert.That((Primary: await Snapshot(MigrationSourceServer.PrimaryDatabase), Throughput: await Snapshot(MigrationSourceServer.ThroughputDatabase)), Is.EqualTo(before), "Neither source database may gain, change or lose anything.");
            Assert.That(Directory.EnumerateFiles(bodyStoragePath, "*", SearchOption.AllDirectories), Is.Empty, "No file may be left in message body storage.");
            Assert.That(await TargetState(), Is.EqualTo(targetBefore), "No row, checkpoint or host-opened record may be written to the target.");
        }

        var (secondExitCode, secondOutput) = await RunAnsweringNoToStartTheCopy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondExitCode, Is.EqualTo(1));
            Assert.That(secondOutput, Does.Contain("Start the copy? [y/N]"), "a run that wrote nothing is still a first run, so the next one asks again");
        }
    }

    async Task<(int ExitCode, string Output)> RunAnsweringNoToStartTheCopy()
    {
        var output = new StringWriter();
        var console = new MigrationConsole(new StringReader(string.Join(Environment.NewLine, "y", "y", "", "", "n")), output, inputRedirected: false, canRedraw: false);

        var exitCode = await MigrateCommand.Run(settings, GuidedMigrationArguments.Prompting, console);

        return (exitCode, output.ToString());
    }

    // Every count the target keeps, so the test compares the whole target before and after rather than one table.
    async Task<string[]> TargetState()
    {
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddPersistence(settings);
        // Never started, so the retention sweeper cannot delete a stray row before this counts it.
        using var host = hostBuilder.Build();

        await using var scope = host.Services.CreateAsyncScope();
        var target = scope.ServiceProvider.GetRequiredService<IMigrationTarget>();
        var state = new List<string>();

        // Only the categories this target writes, because Count throws for any other.
        foreach (var category in target.SupportedCategoryIds.Order(StringComparer.Ordinal).Select(id => MigrationCategoryRegistry.Find(id)!))
        {
            state.Add($"{category.Id} {await target.Count(category)}");
        }

        state.Add($"checkpoints {(await scope.ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().ReadAll()).Count}");
        state.Add($"host opened {await scope.ServiceProvider.GetRequiredService<IMigrationTargetReadiness>().HasHostOpened()}");
        return [.. state];
    }

    // Complete enough that the copy would store it and write its body.
    async Task SeedStorableFailedMessageWithBody()
    {
        const string UniqueMessageId = "8c1d0b3e-5f6a-4b7c-9d8e-0f1a2b3c4d5e";
        const string DocumentId = $"FailedMessages/{UniqueMessageId}";

        using (var seedStore = OpenSourceStore())
        using (var session = seedStore.OpenAsyncSession())
        {
            await session.StoreAsync(new FailedMessage
            {
                UniqueMessageId = UniqueMessageId,
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        MessageId = "answer-no-writes-nothing",
                        AttemptedAt = DateTime.UtcNow,
                        FailureDetails = new FailureDetails { AddressOfFailingEndpoint = "Sales@machine", TimeOfFailure = DateTime.UtcNow }
                    }
                ]
            }, DocumentId);
            session.Advanced.Attachments.Store(DocumentId, "body", new MemoryStream(new byte[128 * 1024]), "text/plain");
            await session.SaveChangesAsync();
        }

        seededDocumentIds.Add(DocumentId);
    }

    async Task Seed(string documentId, object document)
    {
        using var store = OpenSourceStore();
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(document, documentId);
        await session.SaveChangesAsync();

        seededDocumentIds.Add(documentId);
    }

    // Written in the shape the RavenDB persister stores, because this project cannot see the persister's own types.
    async Task SeedSubscription(string typeName, int majorVersion, string transportAddress, string endpoint)
    {
        var entryId = $"Subscriptions/{typeName}/{majorVersion}";

        using var store = OpenSourceStore();
        using var session = store.OpenAsyncSession();
        var document = await session.LoadAsync<SubscriptionsDocument>(SubscriptionsDocumentId) ?? new SubscriptionsDocument();

        if (!document.All.TryGetValue(entryId, out var entry))
        {
            entry = new SubscriptionEntry { Id = entryId, MessageType = new MessageType(typeName, new Version(majorVersion, 0, 0)).ToString() };
            document.All[entryId] = entry;
        }

        entry.Subscribers.Add(new SubscriberEntry { TransportAddress = transportAddress, Endpoint = endpoint });

        await session.StoreAsync(document, SubscriptionsDocumentId);
        await session.SaveChangesAsync();

        seededDocumentIds.Add(SubscriptionsDocumentId);
    }

    // Written in the shape and at the id the RavenDB persister uses, because this project cannot see the persister's own types.
    async Task SeedThroughputEndpoint(string name, global::Particular.LicensingComponent.Contracts.ThroughputSource throughputSource)
    {
        var documentId = $"{name}/{throughputSource}";
        var document = new
        {
            Id = documentId,
            EndpointId = new { Name = name, ThroughputSource = throughputSource },
            SanitizedName = name,
            EndpointIndicators = Array.Empty<string>(),
            UserIndicator = string.Empty,
            Scope = string.Empty
        };

        using var store = OpenSourceStore(MigrationSourceServer.ThroughputDatabase);
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(document, documentId);
        session.Advanced.GetMetadataFor(document)["@collection"] = "EndpointDocuments";
        await session.SaveChangesAsync();

        seededThroughputDocumentIds.Add(documentId);
    }

    // Other fixtures share this source and verify it against an empty target, so nothing a test seeds may outlive it.
    async Task DeleteSeededDocuments()
    {
        try
        {
            await DeleteDocuments(MigrationSourceServer.PrimaryDatabase, seededDocumentIds);
        }
        finally
        {
            await DeleteDocuments(MigrationSourceServer.ThroughputDatabase, seededThroughputDocumentIds);
        }
    }

    static async Task DeleteDocuments(string database, HashSet<string> documentIds)
    {
        using var store = OpenSourceStore(database);
        using var session = store.OpenAsyncSession();

        foreach (var documentId in documentIds)
        {
            session.Delete(documentId);
        }

        await session.SaveChangesAsync();
        documentIds.Clear();
    }

    static IDocumentStore OpenSourceStore() => OpenSourceStore(MigrationSourceServer.PrimaryDatabase);

    static IDocumentStore OpenSourceStore(string database) => new DocumentStore
    {
        Urls = [MigrationSourceServer.ServerUrl],
        Database = database,
        Conventions = new DocumentConventions { SaveEnumsAsIntegers = true }
    }.Initialize();

    void SetVariables()
    {
        bodyStoragePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Bodies", Guid.NewGuid().ToString("n"));
        // Only the installer creates this folder, and the guided migration runs no installer.
        Directory.CreateDirectory(bodyStoragePath);

        SetVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        SetVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING", connectionString);
        SetVariable(MigrationTestTarget.SchemaVariable, schema);
        SetVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        SetVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", bodyStoragePath);
        SetVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", MigrationSourceServer.ServerUrl);
        SetVariable("SERVICECONTROL_RAVENDB_DATABASENAME", MigrationSourceServer.PrimaryDatabase);
        SetVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", MigrationSourceServer.ThroughputDatabase);

        settings = new Settings(persisterType: persisterType, forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));
        MigrationTestTarget.ShareLoadContexts(settings);
    }

    static void DeleteBodyStorage(string path)
    {
        if (path is not null && Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    void ClearVariables()
    {
        foreach (var name in variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        variables.Clear();
    }

    void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        variables.Add(name);
    }

    sealed class SubscriptionsDocument
    {
        public Dictionary<string, SubscriptionEntry> All { get; set; } = [];
    }

    sealed class SubscriptionEntry
    {
        public string Id { get; set; }

        public string MessageType { get; set; }

        public List<SubscriberEntry> Subscribers { get; set; } = [];
    }

    sealed class SubscriberEntry
    {
        public string TransportAddress { get; set; }

        public string Endpoint { get; set; }
    }
}
