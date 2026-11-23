namespace ServiceControl.Migration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Hosting.Commands;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

[TestFixture(MigrationTestTarget.SqlServer)]
[TestFixture(MigrationTestTarget.PostgreSql)]
[NonParallelizable]
class VerifyStepTests(string persisterType)
{
    Settings settings;
    string bodyStoragePath;
    string schema;
    readonly List<string> variables = [];

    [SetUp]
    public async Task SetUp()
    {
        bodyStoragePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Bodies", Guid.NewGuid().ToString("n"));
        schema = MigrationTestTarget.NewSchema();

        SetVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        SetVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING", await MigrationTestTarget.ConnectionString(persisterType));
        SetVariable(MigrationTestTarget.SchemaVariable, schema);
        SetVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        SetVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", bodyStoragePath);
        SetVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", MigrationSourceServer.ServerUrl);
        SetVariable("SERVICECONTROL_RAVENDB_DATABASENAME", MigrationSourceServer.PrimaryDatabase);
        SetVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", MigrationSourceServer.ThroughputDatabase);

        // The source server seeds Sales, and Support is the second row the copy read.
        using (var seedStore = OpenSourceStore())
        using (var session = seedStore.OpenAsyncSession())
        {
            await session.StoreAsync(new EndpointSettings { Name = "Support", TrackInstances = true }, "EndpointSettings/2");
            await session.SaveChangesAsync();
        }

        settings = new Settings(persisterType: persisterType, forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));

        await MigrationTestTarget.ApplySchema(settings);
        await MigrationTestTarget.WithCheckpointStore(settings, async (checkpointStore, cancellationToken) =>
        {
            // Both rows are recorded as read and written, so by default nothing is left to report.
            await checkpointStore.Upsert(new MigrationCheckpoint(
                "EndpointSettings", MigrationCategoryState.Complete, "EndpointSettings/2",
                CopiedCount: 2, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
                StartedAt: DateTime.UtcNow.AddMinutes(-2), LastProgressAt: DateTime.UtcNow.AddMinutes(-1),
                SettledAt: DateTime.UtcNow.AddMinutes(-1), LastError: null), cancellationToken);

            // Every other required category is Done, so only what a test changes can fail the command.
            foreach (var category in MigrationCategoryRegistry.All.Where(category => category.Kind == MigrationCategoryKind.Required && category.Id != "EndpointSettings"))
            {
                await checkpointStore.Upsert(new MigrationCheckpoint(
                    category.Id, MigrationCategoryState.Complete, null,
                    CopiedCount: 0, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
                    StartedAt: DateTime.UtcNow.AddMinutes(-2), LastProgressAt: null,
                    SettledAt: DateTime.UtcNow.AddMinutes(-1), LastError: null), cancellationToken);
            }
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            // The source server is shared with the other fixtures, which expect only the EndpointSettings document it seeds.
            using var sourceStore = OpenSourceStore();
            using var session = sourceStore.OpenAsyncSession();
            session.Delete("EndpointSettings/2");
            await session.SaveChangesAsync();
        }
        finally
        {
            try
            {
                await MigrationTestTarget.DropSchema(persisterType, schema);
            }
            finally
            {
                foreach (var name in variables)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }

                variables.Clear();
            }
        }
    }

    [Test]
    public async Task A_clean_migration_exits_0_lists_no_optional_category_and_reads_a_row_this_source_cannot_count_as_not_counted()
    {
        // The source holds no redirects, so abandoning them loses nothing.
        await UpdateCheckpoint("MessageRedirects", checkpoint => checkpoint with { State = MigrationCategoryState.Abandoned });

        // A newer build wrote this row, and this source has no reader for it.
        await MigrationTestTarget.WithCheckpointStore(settings, (checkpointStore, cancellationToken) => checkpointStore.Upsert(new MigrationCheckpoint(
            "CategoryFromANewerBuild", MigrationCategoryState.Complete, null,
            CopiedCount: 4, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
            StartedAt: DateTime.UtcNow.AddMinutes(-2), LastProgressAt: DateTime.UtcNow.AddMinutes(-1),
            SettledAt: DateTime.UtcNow.AddMinutes(-1), LastError: null), cancellationToken));

        var report = await Capture(expectedExitCode: 0);
        var lines = report.Split('\n');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report, Does.Contain("Every required category is Done or Abandoned."));
            Assert.That(report, Does.Not.Match(@"(?m)^(EventLog|ArchivedAndResolvedFailedMessages)\s"), "ServicePulse shows the optional categories, so verify does not list them");
            Assert.That(lines.Single(text => text.StartsWith("MessageRedirects")), Does.Contain("ABANDONED"), "the operator chose to abandon it, and the report still shows it");
            Assert.That(lines.Single(text => text.StartsWith("CategoryFromANewerBuild")), Does.Match(@"^CategoryFromANewerBuild\s+-\s+4\s.*\s-\s+NOT COUNTED"), "nothing counted the source, so its count and the unaccounted rows are dashes");
        }
    }

    [Test]
    public async Task An_outstanding_category_or_a_failed_row_under_an_unknown_id_exits_1_even_when_rows_were_not_read()
    {
        await ReadOnlySales();
        await UpdateCheckpoint("MessageRedirects", checkpoint => checkpoint with { State = MigrationCategoryState.Halted, SettledAt = null, LastError = "test" });
        await MigrationTestTarget.WithCheckpointStore(settings, (checkpointStore, cancellationToken) => checkpointStore.Upsert(new MigrationCheckpoint(
            "CategoryFromANewerBuild", MigrationCategoryState.Halted, null,
            CopiedCount: 3, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
            StartedAt: DateTime.UtcNow.AddMinutes(-2), LastProgressAt: DateTime.UtcNow.AddMinutes(-1),
            SettledAt: null, LastError: "test"), cancellationToken));

        var (report, exitCode) = await Run();
        var lines = report.Split('\n');

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines.Single(text => text.StartsWith("EndpointSettings")), Does.Contain("ROWS NOT READ"));
            Assert.That(lines.Single(text => text.StartsWith("MessageRedirects")), Does.Contain("INCOMPLETE"));
            Assert.That(lines.SingleOrDefault(text => text.StartsWith("CategoryFromANewerBuild")) ?? "", Does.Contain("INCOMPLETE"));
            Assert.That(report, Does.Contain("2 categories are INCOMPLETE"), "the start guard refuses the unknown row too, so it is a fault of its own");
            Assert.That(exitCode, Is.EqualTo(1), "an unfinished migration outranks a finished one that needs a look");
        }
    }

    [Test]
    public async Task Skip_reasons_and_an_abandoned_loss_are_each_printed_under_the_table()
    {
        // The source holds 2 endpoint settings: 1 skipped, so 1 never read.
        await UpdateCheckpoint("EndpointSettings", checkpoint => checkpoint with
        {
            State = MigrationCategoryState.Abandoned,
            CopiedCount = 0,
            SkippedCount = 1,
            SkipReasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 1 }
        });

        // The source holds no redirects, and every count differs from the others so a swapped column shows.
        await UpdateCheckpoint("MessageRedirects", checkpoint => checkpoint with
        {
            CopiedCount = 8,
            SkippedCount = 5,
            AlreadyPresentCount = 3,
            SkipReasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.SourceStorageCheck] = 5 }
        });

        var report = await Capture(expectedExitCode: 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report, Does.Match(@"(?m)^MessageRedirects\s+0\s+8\s+5\s+3\s+-16\s+OK\r?$"), "Source, Copied, Skipped, Present and Unaccounted, in that order");
            Assert.That(report, Does.Match(@"(?m)^  EndpointSettings\s+RequiredValueMissing\s+1, no retry can fix\r?$"), "verify marks a reason as the start refusal does");
            Assert.That(report, Does.Match(@"(?m)^  MessageRedirects\s+SourceStorageCheck\s+5, harmless\r?$"));
            Assert.That(report, Does.Match(@"(?m)^Abandoned categories: .*\r?\n.*\r?\n  EndpointSettings\s+1\r?$"), "the rows the last pass never read");
        }
    }

    async Task<string> Capture(int expectedExitCode)
    {
        var (report, exitCode) = await Run();

        Assert.That(exitCode, Is.EqualTo(expectedExitCode), "the report returned the wrong exit code");
        return report;
    }

    async Task<(string Report, int ExitCode)> Run()
    {
        // Never start this host, because starting it would run the retention sweeper the persistence registers.
        await using var app = MigrationCommandHost.CreateBuilder(settings).Build();
        await using var source = await PersistenceFactory.OpenMigrationSource(settings);

        var store = app.Services.GetRequiredService<IMigrationCheckpointStore>();
        var checkpoints = await store.ReadAll();
        var output = new StringWriter();

        var exitCode = await VerifyStep.Report(
            new MigrationConsole(TextReader.Null, output, inputRedirected: true, canRedraw: false),
            checkpoints,
            source,
            new Dictionary<string, long>());

        return (output.ToString(), exitCode);
    }

    // Only Sales is recorded as read, so the source holds exactly one EndpointSettings row the copy never read.
    Task ReadOnlySales() =>
        UpdateCheckpoint("EndpointSettings", checkpoint => checkpoint with { Cursor = "EndpointSettings/1", CopiedCount = 1 });

    Task UpdateCheckpoint(string categoryId, Func<MigrationCheckpoint, MigrationCheckpoint> change) =>
        MigrationTestTarget.WithCheckpointStore(settings, async (checkpointStore, cancellationToken) =>
            await checkpointStore.Upsert(change(await checkpointStore.Read(categoryId, cancellationToken)), cancellationToken));

    static IDocumentStore OpenSourceStore() => new DocumentStore
    {
        Urls = [MigrationSourceServer.ServerUrl],
        Database = MigrationSourceServer.PrimaryDatabase,
        Conventions = new DocumentConventions { SaveEnumsAsIntegers = true }
    }.Initialize();

    void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        variables.Add(name);
    }
}
