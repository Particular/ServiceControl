namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class RowsToCopyStepTests
{
    [Test]
    public async Task The_rows_step_counts_each_category_once_and_the_copy_saves_that_total_first_and_ends_with_a_line_per_category()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"), GuidedMigrationHarness.Row("KnownEndpoints/2"));
        var copyableRequired = MigrationCategoryRegistry.All
            .Where(category => category.Kind == MigrationCategoryKind.Required)
            .Select(category => category.Id)
            .Intersect(MigrationStartup.CopyableCategoryIds(harness.Source.SupportedCategoryIds, harness.Target.SupportedCategoryIds));
        long? totalAtFirstWrite = null;
        harness.Target.BeforeWrite = checkpoint => totalAtFirstWrite ??= checkpoint.SourceTotal;

        await harness.Run(GuidedMigrationArguments.Unattended);

        var copyStep = harness.Output[harness.Output.IndexOf("Copying required data", StringComparison.Ordinal)..];
        var shown = Regex.Matches(copyStep, @"(?m)^   (\S+)\s+\d+ / ").Select(match => match.Groups[1].Value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(harness.StepHeadings, Does.Contain("3. Rows to copy"));
            Assert.That(harness.Output, Does.Match(@"(?m)^   KnownEndpoints\s+2\r?$"));
            Assert.That(copyableRequired.Select(id => harness.Source.CountedCategories.Count(counted => counted == id)), Is.Not.Empty.And.All.EqualTo(1),
                "each copyable required category is counted exactly once, by step 3, so an empty list fails too");
            Assert.That(totalAtFirstWrite, Is.EqualTo(2), "the source total is saved before the first row is written");
            Assert.That(copyStep, Does.Match(@"(?m)^   KnownEndpoints\s+2 / 2\s+100%  Done\s*$"));
            Assert.That(shown, Is.EqualTo(MigrationCategoryRegistry.All.Where(category => category.Kind == MigrationCategoryKind.Required).OrderBy(category => category.Order).Select(category => category.Id)));
        }
    }

    [Test]
    public async Task Over_the_limit_a_wrong_abandon_word_asks_again_and_stop_lists_first_and_writes_nothing()
    {
        var harness = OverTheLimit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "a", "abandon", "s"), Is.EqualTo(1));
            Assert.That(harness.Output, Does.Contain("WARNING: there are 182,344 unresolved failed messages, more than 50,000."));
            Assert.That(harness.Output.IndexOf("[s] Stop now"), Is.Not.EqualTo(-1));
            Assert.That(harness.Output.IndexOf("[c] Continue"), Is.Not.EqualTo(-1));
            Assert.That(harness.Output.IndexOf("[s] Stop now"), Is.LessThan(harness.Output.IndexOf("[c] Continue")));
            Assert.That(harness.Output.Split("Choice:"), Has.Length.EqualTo(3), "the choice is asked once, then again after the wrong word");
            Assert.That(AfterTheLastChoice(harness.Output), Does.StartWith(" Nothing has been written, and you can go back to RavenDB."));
            Assert.That(harness.Output, Does.Contain("1. Set PersistenceType back to RavenDB, then start ServiceControl."),
                "the instance is already configured for SQL, so starting it as it is opens it on the empty target");
            Assert.That(await harness.Store.ReadAll(), Is.Empty);
        }
    }

    [Test]
    public async Task A_resume_lists_only_the_categories_that_are_not_settled()
    {
        var harness = new GuidedMigrationHarness();
        await harness.Store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete, null, 0, 0, null, null, null, null, DateTime.UtcNow, null));

        await harness.Run(GuidedMigrationArguments.Unattended);
        var rowsStep = harness.Output.Split("Rows to copy")[1].Split("Copying required data")[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rowsStep, Does.Not.Contain("KnownEndpoints"));
            Assert.That(rowsStep, Does.Contain("EndpointSettings"));
        }
    }

    [Test]
    public async Task Continue_copies_the_category()
    {
        var harness = OverTheLimit();
        harness.Source.Seed(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, GuidedMigrationHarness.Row("FailedMessages/1"));
        harness.Source.SetBody("FailedMessages/1", new MigrationBody(new byte[] { 1 }, "text/plain"));

        await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "c", "y");

        // The exit code is 2 here, because the overridden count is larger than the one row seeded.
        Assert.That((await harness.Store.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages))!.State, Is.EqualTo(MigrationCategoryState.Complete));
    }

    [Test]
    public async Task An_abandon_is_written_only_after_start_the_copy()
    {
        var harness = OverTheLimit();

        Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "a", "ABANDON", "n"), Is.EqualTo(1));
        Assert.That(await harness.Store.ReadAll(), Is.Empty, "the operator said no to the copy, so the abandon is not written");

        Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "a", "ABANDON", "y"), Is.Zero);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await harness.Store.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That(harness.Output, Does.Contain("They stay only in RavenDB."));
        }
    }

    [Test]
    public void A_run_without_a_console_one_over_the_limit_stops_naming_both_ways_past_it() =>
        Assert.That(async () => await OverTheLimit(count: 50_001).Run(GuidedMigrationArguments.Unattended),
            Throws.Exception.With.Message.Contain("There are 50,001 unresolved failed messages").And.Message.Contain("Archive or resolve").And.Message.Contain("--abandon=UnresolvedAndRetryIssuedFailedMessages"));

    [Test]
    public async Task An_abandon_argument_abandons_the_category_over_the_limit()
    {
        var arguments = GuidedMigrationArguments.Unattended with { Abandon = new HashSet<string> { MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages } };
        var harness = OverTheLimit();

        await harness.Run(arguments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await harness.Store.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That(harness.Output, Does.Contain("UnresolvedAndRetryIssuedFailedMessages will be abandoned when the copy starts (--abandon=UnresolvedAndRetryIssuedFailedMessages)."));
            Assert.That(harness.Output, Does.Match(@"(?m)^   UnresolvedAndRetryIssuedFailedMessages\s+0\s+Abandoned\s*\r?$"),
                "an abandoned category stops for good, so its progress line shows no share of the source, as --migration-status does");
        }
    }

    [Test]
    public async Task A_resume_with_an_abandon_argument_abandons_the_category_before_it_has_started()
    {
        var arguments = GuidedMigrationArguments.Unattended with { Abandon = new HashSet<string> { MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages } };
        var harness = OverTheLimit();
        await harness.Store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete, null, 0, 0, null, null, DateTime.UtcNow, null, DateTime.UtcNow, null));
        await harness.Store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, null));

        await harness.Run(arguments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await harness.Store.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That(harness.Output, Does.Contain("UnresolvedAndRetryIssuedFailedMessages will be abandoned when the copy starts (--abandon=UnresolvedAndRetryIssuedFailedMessages)."));
            Assert.That(harness.Output, Does.Not.Contain("WARNING"), "a resume keeps the first run's warning and acts only on the argument");
        }
    }

    [Test]
    public async Task A_resume_with_an_abandon_argument_copies_the_category_once_it_has_started()
    {
        var arguments = GuidedMigrationArguments.Unattended with { Abandon = new HashSet<string> { MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages } };
        var harness = OverTheLimit();
        var startedAt = harness.Clock.GetUtcNow().UtcDateTime;
        await harness.Store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryState.InProgress, null, 0, 0, null, null, startedAt, startedAt, null, null));

        await harness.Run(arguments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await harness.Store.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages))!.State, Is.Not.EqualTo(MigrationCategoryState.Abandoned),
                "the copy had started, so abandoning it now would leave the rows it has not copied only in RavenDB");
            Assert.That(harness.Output, Does.Not.Contain("will be abandoned when the copy starts"));
        }
    }

    // The [s] label repeats the stop text, so only what follows the prompt is the message printed after the choice.
    static string AfterTheLastChoice(string output) => output.Split("Choice:")[^1];

    static GuidedMigrationHarness OverTheLimit(long count = 182_344)
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.CountOverrides[MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages] = count;
        return harness;
    }
}
