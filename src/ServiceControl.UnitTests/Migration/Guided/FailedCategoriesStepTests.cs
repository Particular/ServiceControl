namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class FailedCategoriesStepTests
{
    [Test]
    public async Task Retry_copies_the_failed_category_again_in_the_same_run()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        harness.Target.FailOnCallNumber = 1;

        var exitCode = await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "y", "r");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That((await harness.Store.Read(MigrationCategoryIds.KnownEndpoints))!.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(harness.Output, Does.Contain("[r] Retry now").And.Contain("[a] Abandon").And.Contain("[s] Stop"));
            Assert.That(harness.StepHeadings, Is.EqualTo(new[] { "1. Configuration", "2. Checks", "3. Rows to copy", "4. Start the copy", "5. Copying required data", "6. Failed categories", "7. Verify" }));
        }
    }

    [Test]
    public async Task Abandon_needs_the_typed_word_and_takes_its_dependants_with_it()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        harness.Target.FailOnCallNumber = 1;

        var exitCode = await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "y", "a", "abandon", "a", "ABANDON");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That((await harness.Store.Read(MigrationCategoryIds.KnownEndpoints))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That((await harness.Store.Read(MigrationCategoryIds.EndpointSettings))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That(harness.Output, Does.Contain("EndpointSettings is abandoned, because it needs the data of KnownEndpoints"));
            Assert.That(Regex.Matches(harness.Output, "Type ABANDON to confirm:").Count, Is.EqualTo(2), "the word typed in lower case abandons nothing, so the choice is asked again");
            Assert.That(harness.Output.IndexOf("Abandoning KnownEndpoints also abandons EndpointSettings, because it needs its data.", StringComparison.Ordinal),
                Is.InRange(0, harness.Output.IndexOf("Type ABANDON to confirm:", StringComparison.Ordinal)), "Abandoned is final, so the operator is told what goes with it before typing the word");
        }
    }

    [Test]
    public async Task Stop_exits_1_and_the_next_run_offers_the_choice_again()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        harness.Target.FailOnCallNumber = 1;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "y", "s"), Is.EqualTo(1));
            Assert.That(await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "r"), Is.Zero, "a resume asks no start question and comes back to the choice");
        }
    }

    [Test]
    public void A_run_without_a_console_stops_on_a_failed_category_naming_both_arguments()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        harness.Target.FailOnCallNumber = 1;

        Assert.That(async () => await harness.Run(GuidedMigrationArguments.Unattended),
            Throws.Exception.With.Message.Contain("nothing has been lost").And.Message.Contain("--retry=KnownEndpoints").And.Message.Contain("--abandon=KnownEndpoints"));
    }

    [Test]
    public void A_retry_argument_is_used_once_per_run()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        var writes = 0;
        // A third write throws what the engine never catches, so a retry used on every pass fails here instead of looping.
        harness.Target.BeforeWrite = _ => throw (++writes > 2 ? new MigrationCheckpointConflictException("a third write") : new InvalidOperationException("still broken"));
        var arguments = GuidedMigrationArguments.Unattended with { Retry = new HashSet<string> { MigrationCategoryIds.KnownEndpoints } };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await harness.Run(arguments),
                Throws.Exception.With.Message.Contain("--abandon=KnownEndpoints"), "the second failure stops the run instead of retrying again");
            Assert.That(writes, Is.EqualTo(2), "the first copy, then exactly one retry");
            Assert.That(harness.Output, Does.Contain("Retrying KnownEndpoints (--retry=KnownEndpoints)."));
        }
    }

    [Test]
    public async Task A_category_named_by_both_arguments_is_retried_once_and_then_abandoned()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        var writes = 0;
        harness.Target.BeforeWrite = _ => throw new InvalidOperationException($"still broken on write {++writes}");
        var both = new HashSet<string> { MigrationCategoryIds.KnownEndpoints };

        await harness.Run(GuidedMigrationArguments.Unattended with { Retry = both, Abandon = both });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(writes, Is.EqualTo(2), "the retry comes first, and the abandon only after it failed");
            Assert.That((await harness.Store.Read(MigrationCategoryIds.KnownEndpoints))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
        }
    }

    [Test]
    public async Task A_refused_retry_is_shown_with_its_cause_and_the_choice_is_asked_again()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        harness.Target.FailOnCallNumber = 1;
        harness.Target.RefuseRetryWith = new InvalidOperationException("KnownEndpoints cannot be retried.", new TimeoutException("the database did not answer"));

        var exitCode = await harness.Run(GuidedMigrationArguments.Prompting, "y", "y", "y", "r", "s");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(harness.Output, Does.Contain("InvalidOperationException: KnownEndpoints cannot be retried.").And.Contain("TimeoutException: the database did not answer"));
            Assert.That(Regex.Matches(harness.Output, @"\[r\] Retry now").Count, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task An_abandon_argument_abandons_only_a_failed_category()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"));
        harness.Source.Seed(MigrationCategoryIds.MessageRedirects, GuidedMigrationHarness.Row("MessageRedirects/1"));
        harness.Target.FailOnCallNumber = 1;
        var arguments = GuidedMigrationArguments.Unattended with { Abandon = new HashSet<string> { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.MessageRedirects } };

        await harness.Run(arguments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await harness.Store.Read(MigrationCategoryIds.KnownEndpoints))!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            Assert.That((await harness.Store.Read(MigrationCategoryIds.MessageRedirects))!.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(harness.Output, Does.Contain("Abandoning KnownEndpoints (--abandon=KnownEndpoints).").And.Contain("Abandoning KnownEndpoints also abandons EndpointSettings"));
            Assert.That(harness.Output, Does.Not.Contain("Abandoning MessageRedirects"), "an argument the run had no use for says nothing");
        }
    }

    [Test]
    public void The_report_names_what_each_abandoned_category_kept_and_why_it_was_abandoned()
    {
        var settings = new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.Abandoned, null, 0, 0, null, null, null, null, null, null);
        var knownEndpoints = new MigrationCheckpoint("KnownEndpoints", MigrationCategoryState.Abandoned, "cursor-40", 40, 3, null, null, null, null, null, "timed out");

        Assert.That(FailedCategoriesStep.AbandonReport([settings, knownEndpoints]), Is.EqualTo(new[]
        {
            "EndpointSettings is abandoned, because it needs the data of KnownEndpoints, with 0 rows copied and 0 skipped. Whatever it had not copied stays only in RavenDB.",
            "KnownEndpoints is abandoned, with 40 rows copied and 3 skipped. Whatever it had not copied stays only in RavenDB."
        }), "these lines are the only place the operator learns what the cascade gave up");
    }

    [Test]
    public void The_report_counts_rows_an_earlier_pass_left_in_the_target()
    {
        // A retry resets the copied count, and the next pass finds the first pass's rows already in the target.
        var retried = new MigrationCheckpoint("UnresolvedAndRetryIssuedFailedMessages", MigrationCategoryState.Abandoned, "cursor-33", 0, 15, null, null, null, null, null, null) with { AlreadyPresentCount = 18 };

        Assert.That(FailedCategoriesStep.AbandonReport([retried]), Is.EqualTo(new[]
        {
            "UnresolvedAndRetryIssuedFailedMessages is abandoned, with 0 rows copied and 15 skipped, and 18 already in the target. Whatever it had not copied stays only in RavenDB."
        }));
    }
}
