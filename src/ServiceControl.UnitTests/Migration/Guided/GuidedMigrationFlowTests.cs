namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Hosting.Commands;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class GuidedMigrationFlowTests
{
    [Test]
    public async Task A_first_run_numbers_only_the_steps_that_ran()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));

        var exitCode = await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "y");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(harness.StepHeadings, Is.EqualTo(new[] { "1. Configuration", "2. Checks", "3. Rows to copy", "4. Start the copy", "5. Copying required data", "6. Verify" }));
            Assert.That(harness.Output, Does.Contain("Stop ServiceControl and every ingestion worker. Continue? [y/N]"));
        }
    }

    [Test]
    public async Task Answering_no_to_the_stop_question_opens_nothing()
    {
        var harness = new GuidedMigrationHarness();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "n"), Is.EqualTo(1));
            Assert.That(harness.Readiness.ChecksRequested, Is.Zero);
            Assert.That(harness.Source.Opened, Is.False);
        }
    }

    [Test]
    public async Task Answering_no_to_start_the_copy_writes_nothing_and_exits_1()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "n"), Is.EqualTo(1));
            Assert.That(await harness.Store.ReadAll(), Is.Empty, "no checkpoint is seeded before the operator says yes");
            Assert.That(MigrationCategoryRegistry.All.SelectMany(category => harness.Target.RowsHandedToWrite(category.Id)), Is.Empty, "no category is handed a row to write");
            Assert.That(harness.Output, Does.Contain("Nothing has been written."));
        }
    }

    [Test]
    public async Task A_resume_copies_without_asking_to_start_the_copy()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        var startedAt = harness.Clock.GetUtcNow().UtcDateTime;
        await harness.Store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress, null, 0, 0, null, null, startedAt, startedAt, null, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y"), Is.Zero);
            Assert.That(harness.StepHeadings, Is.EqualTo(new[] { "1. Configuration", "2. Checks", "3. Rows to copy", "4. Copying required data", "5. Verify" }));
            Assert.That(harness.Target.WrittenRows(MigrationCategoryIds.KnownEndpoints), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task A_verify_that_finds_rows_not_read_ends_the_run_with_2()
    {
        var harness = new GuidedMigrationHarness();
        await harness.SettleEveryRequiredCategory();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Unattended), Is.EqualTo(2), "the source holds a row the settled copy never read");
            Assert.That(harness.Output, Does.Contain("ROWS NOT READ"));
        }
    }

    [Test]
    public async Task A_settled_run_whose_source_will_not_open_writes_no_optional_row_skips_verify_and_exits_2()
    {
        var harness = new GuidedMigrationHarness();
        await harness.SettleEveryRequiredCategory();
        harness.Source.OpenFailure = new TimeoutException("RavenDB stopped answering");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Unattended), Is.EqualTo(2));
            Assert.That(harness.Output, Does.Match(@"(?m)^   \[skip\] the migration source opens .*RavenDB stopped answering"));
            Assert.That(harness.Output, Does.Contain("Verify skipped"));
            Assert.That(Regex.Matches(harness.Output, "RavenDB stopped answering").Count, Is.EqualTo(1), "the skip line already gives the reason, which can be RavenDB's whole server output");
            Assert.That((await harness.Store.ReadAll()).Where(checkpoint => MigrationCategoryRegistry.KindOf(checkpoint.CategoryId) == MigrationCategoryKind.Optional), Is.Empty,
                "this build copies no optional category, so a row for one would wait for a copy that never runs");
        }
    }

    [Test]
    public async Task A_settled_run_neither_counts_nor_judges_an_optional_category_ServicePulse_started()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Unsupported.Clear();
        await harness.SettleEveryRequiredCategory();
        var startedAt = harness.Clock.GetUtcNow().UtcDateTime;
        await harness.Store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.EventLog, MigrationCategoryState.InProgress, null, 0, 0, null, null, startedAt, startedAt, null, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Unattended), Is.Zero, "an optional category still copying belongs to ServicePulse, not to the offline command");
            Assert.That(harness.Source.CountedCategories, Does.Not.Contain(MigrationCategoryIds.EventLog), "counting it would read the whole category for nothing");
            Assert.That(harness.Output, Does.Not.Match(@"(?m)^EventLog\s"));
        }
    }

    [Test]
    public async Task The_offline_command_saves_no_optional_row_and_a_settled_run_still_exits_0()
    {
        var harness = new GuidedMigrationHarness();
        // This build's source cannot read the optional categories, so this stands in for a later source that can.
        harness.Source.Unsupported.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Unattended), Is.Zero, "the optional categories have not started, and that is not a fault");
            Assert.That(await harness.Store.Read(MigrationCategoryIds.EventLog), Is.Null, "ServicePulse starts the optional categories, so the offline command saves no row for one");
            Assert.That(harness.Output, Does.Not.Match(@"(?m)^EventLog\s"), "ServicePulse shows the optional categories, so verify does not list them");
            Assert.That(harness.Output, Does.Contain("Every required category is Done or Abandoned.").And.Not.Contain("Every selected category"));

            Assert.That(await harness.Run(GuidedMigrationArguments.Unattended), Is.Zero, "the finish run must not wait for optional data");
            Assert.That(harness.StepHeadings, Is.EqualTo(new[] { "1. Configuration", "2. Checks", "3. Verify" }));
        }
    }

    [Test]
    public void A_RavenDB_target_is_refused_naming_PersistenceType_before_any_service_is_resolved() =>
        Assert.That(async () => await MigrateCommand.Run(new Settings(persisterType: PersistenceFactory.MigrationSourcePersistenceType), GuidedMigrationArguments.Unattended,
                new MigrationConsole(TextReader.Null, new StringWriter(), inputRedirected: true, canRedraw: false)),
            Throws.Exception.With.Message.Contain("PersistenceType"), "the RavenDB persistence registers no migration target, so resolving one first would hide the advice");
}
