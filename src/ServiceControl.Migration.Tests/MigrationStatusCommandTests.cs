namespace ServiceControl.Migration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;

[TestFixture(MigrationTestTarget.SqlServer)]
[TestFixture(MigrationTestTarget.PostgreSql)]
[NonParallelizable]
class MigrationStatusCommandTests(string persisterType)
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

        // The command fails through the process-wide exit code, so each test starts from 0.
        Environment.ExitCode = 0;

        settings = new Settings(persisterType: persisterType, forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));

        await MigrationTestTarget.ApplySchema(settings);
        await MigrationTestTarget.WithCheckpointStore(settings, async (checkpointStore, cancellationToken) =>
        {
            await checkpointStore.Upsert(new MigrationCheckpoint(
                "ArchivedAndResolvedFailedMessages", MigrationCategoryState.InProgress, "FailedMessages/9",
                CopiedCount: 240, SkippedCount: 10, SourceTotal: null, SkipReasons: new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 10 },
                StartedAt: DateTime.UtcNow.AddMinutes(-20), LastProgressAt: DateTime.UtcNow.AddMinutes(-1),
                SettledAt: null, LastError: null), cancellationToken);
            await checkpointStore.Upsert(new MigrationCheckpoint(
                "EventLog", MigrationCategoryState.InProgress, "EventLogItems/9",
                CopiedCount: 9, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
                StartedAt: DateTime.UtcNow.AddMinutes(-20), LastProgressAt: DateTime.UtcNow.AddMinutes(-5),
                SettledAt: null, LastError: "TimeoutException at cursor EventLogItems/9: RavenDB stopped answering"), cancellationToken);
            await checkpointStore.Upsert(new MigrationCheckpoint(
                "MessageRedirects", MigrationCategoryState.Halted, "MessageRedirects/4",
                CopiedCount: 4, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
                StartedAt: DateTime.UtcNow.AddMinutes(-30), LastProgressAt: DateTime.UtcNow.AddMinutes(-25),
                SettledAt: DateTime.UtcNow.AddMinutes(-25), LastError: "TimeoutException at cursor MessageRedirects/4: RavenDB stopped answering"), cancellationToken);
        });
    }

    [TearDown]
    public async Task TearDown()
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
            Environment.ExitCode = 0;
        }
    }

    [Test]
    public async Task The_report_shows_each_seeded_row_and_nothing_else_on_standard_output()
    {
        // A source nothing listens on, so any open fails, as it would on a source another process holds open.
        SetVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", "http://localhost:1");

        var report = await Capture();
        var lines = report.Split('\n');
        var archive = lines.Single(text => text.StartsWith("ArchivedAndResolvedFailedMessages"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report, Does.Contain("The source is never opened"));
            Assert.That(archive, Does.Contain("Copying (InProgress)"));
            Assert.That(archive, Does.Match(@"240\s+10\s+0\s*$"), "copied, skipped and already present, in that order");
            Assert.That(report, Does.Contain("Present is rows the target already held"));
            Assert.That(archive, Does.Not.Contain("%"), "the row has no source total, so there is no percentage to print");
            Assert.That(report, Does.Contain("Failed (Halted)"));
            Assert.That(report, Does.Contain("last error: TimeoutException at cursor MessageRedirects/4: RavenDB stopped answering"));
            Assert.That(report, Does.Contain("stays Failed on every start until you stop ServiceControl and run: --start-data-migration --retry=MessageRedirects  or  --start-data-migration --abandon=MessageRedirects"), "the engine's error names no command, so status is where the operator reads them");
            Assert.That(report.Split("only --start-data-migration").Length - 1, Is.EqualTo(1), "MessageRedirects is the one required category with a row, and a required category with no row does not stop a start");
            Assert.That(report, Does.Contain("last error: TimeoutException at cursor EventLogItems/9: RavenDB stopped answering"));
            Assert.That(report, Does.Not.Contain("--abandon=EventLog"), "the guided run offers no command for an optional category");
            Assert.That(lines.Single(text => text.StartsWith("KnownEndpoints")), Does.Match(@"^KnownEndpoints\s+Required\s+yes\s+Not started\s+0\s+0\s+0\s*$"), "SetUp gives it no row, so nothing has written to it and it must not read like a copy under way");
            Assert.That(report.Split(Environment.NewLine), Has.None.Match(@"^(trce|dbug|info|warn|fail|crit): "), "a log line in the middle of the report would break the table");
        }
    }

    [Test]
    public async Task The_done_column_skips_an_abandoned_row_and_a_row_an_outage_left_before_the_category_started_reads_not_started()
    {
        await MigrationTestTarget.WithCheckpointStore(settings, async (checkpointStore, cancellationToken) =>
        {
            await checkpointStore.Upsert(new MigrationCheckpoint(
                "KnownEndpoints", MigrationCategoryState.InProgress, "KnownEndpoints/4",
                CopiedCount: 2, SkippedCount: 1, SourceTotal: 8, SkipReasons: new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 1 },
                StartedAt: DateTime.UtcNow.AddMinutes(-2), LastProgressAt: DateTime.UtcNow.AddMinutes(-1),
                SettledAt: null, LastError: null, AlreadyPresentCount: 1), cancellationToken);
            await checkpointStore.Upsert(new MigrationCheckpoint(
                "LicensingEndpoints", MigrationCategoryState.Abandoned, null,
                CopiedCount: 0, SkippedCount: 0, SourceTotal: 8, SkipReasons: null,
                StartedAt: null, LastProgressAt: null,
                SettledAt: DateTime.UtcNow.AddMinutes(-1), LastError: null), cancellationToken);

            var seeded = (await checkpointStore.ReadAll(cancellationToken)).Single(checkpoint => checkpoint.CategoryId == "ArchivedAndResolvedFailedMessages");
            await checkpointStore.Upsert(seeded with
            {
                State = MigrationCategoryState.NotStarted,
                Cursor = null,
                CopiedCount = 0,
                SkippedCount = 0,
                SkipReasons = null,
                StartedAt = null,
                LastProgressAt = null,
                LastError = "The source could not be opened: RavenDB did not answer"
            }, cancellationToken);
        });

        var lines = (await Capture()).Split(Environment.NewLine);
        var archive = Array.FindIndex(lines, text => text.StartsWith("ArchivedAndResolvedFailedMessages"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines.Single(text => text.StartsWith("KnownEndpoints")), Does.Match(@"\s2\s+1\s+1\s+50%\s*$"), "skipped and already present rows count as done, because the source count includes them");
            Assert.That(lines.Single(text => text.StartsWith("LicensingEndpoints")), Does.Contain("Abandoned").And.Not.Contain("%"), "an abandoned category is not copied further, so a share would read as a copy still to finish");
            Assert.That(lines[archive], Does.Match(@"^ArchivedAndResolvedFailedMessages\s+Optional\s+no\s+Not started\s"), "nothing has copied it yet, so it must not read like a copy under way");
            Assert.That(lines[archive + 1], Is.EqualTo("  started not yet, last progress not yet, settled not yet"));
            Assert.That(lines[archive + 2], Is.EqualTo("  last error: The source could not be opened: RavenDB did not answer"));
        }
    }

    [Test]
    public async Task An_instance_whose_persistence_keeps_no_checkpoints_is_refused_in_one_line()
    {
        var ravenSettings = new Settings(persisterType: "RavenDB", forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));

        var (report, errors) = await CaptureOut(cancellationToken => new MigrationStatusCommand().Execute(new HostArguments([]), ravenSettings, cancellationToken));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Does.Contain("the RavenDB persistence keeps no migration checkpoints"));
            Assert.That(Environment.ExitCode, Is.EqualTo(1));
            Assert.That(report, Does.Not.Contain("ServiceControl migration status"), "the refusal comes before the report, so it cannot be mistaken for an empty one");
        }
    }

    // Execute reports a failure only on standard error and in the exit code, so a report alone cannot show that it succeeded.
    async Task<string> Capture()
    {
        var (report, errors) = await CaptureOut(cancellationToken => new MigrationStatusCommand().Execute(new HostArguments([]), settings, cancellationToken));

        Assert.That((Environment.ExitCode, errors), Is.EqualTo((0, "")), "the command wrote an error or set a failing exit code");
        return report;
    }

    static async Task<(string Out, string Error)> CaptureOut(Func<CancellationToken, Task> run)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var originalOutput = Console.Out;
        var originalErrors = Console.Error;
        Console.SetOut(output);
        Console.SetError(errors);

        try
        {
            await run(CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalErrors);
        }

        return (output.ToString(), errors.ToString());
    }

    void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        variables.Add(name);
    }
}
