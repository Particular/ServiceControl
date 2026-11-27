#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class RequiredCopyGateTests
{
    [TestCase(MigrationCategoryState.Complete)]
    public void A_finished_required_category_lets_the_host_open(MigrationCategoryState state) =>
        Assert.DoesNotThrow(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(state)], NewSettings()));

    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.NotStarted)]
    // Blocked happens because EndpointSettings must follow KnownEndpoints.
    [TestCase(MigrationCategoryState.Blocked)]
    public void A_required_category_that_is_not_finished_keeps_the_host_closed(MigrationCategoryState state)
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(state)], NewSettings()));

        Assert.That(exception.Message, Does.Contain(MigrationCategoryIds.EndpointSettings).And.Contain(state.ToString()));
    }

    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.Halted)]
    public void An_unfinished_row_under_an_id_this_build_does_not_know_keeps_the_host_closed(MigrationCategoryState state)
    {
        var unknown = Checkpoint(state, UnknownCategoryId);

        var outside = MigrationStartup.UnfinishedRowsOutsideTheCopy([unknown, Checkpoint(MigrationCategoryState.Complete)], [EndpointSettings]);
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(MigrationCategoryState.Complete), .. outside], NewSettings()));

        Assert.That(exception.Message, Does.Contain($"{UnknownCategoryId} is ").And.Contain(state.ToString()),
            "an id this build does not know is required until it is known to be optional, as the ingestion gate already decides");
    }

    [Test]
    public void An_unfinished_unknown_row_makes_the_copy_not_settled_and_the_refusal_names_it()
    {
        var unknown = Checkpoint(MigrationCategoryState.InProgress, UnknownCategoryId);
        var checkpoints = new[] { Checkpoint(MigrationCategoryState.Complete), unknown };
        var outside = MigrationStartup.UnfinishedRowsOutsideTheCopy(checkpoints, [EndpointSettings]);

        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(MigrationCategoryState.Complete)], outside, NewSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(MigrationStartup.RequiredCopyIsSettled(checkpoints, [EndpointSettings]), Is.False);
            Assert.That(exception.Message, Does.Contain($"{UnknownCategoryId} is InProgress"));
        });
    }

    [Test]
    public void A_finished_unknown_row_leaves_the_copy_settled_and_the_refusal_silent()
    {
        var checkpoints = new[] { Checkpoint(MigrationCategoryState.Complete), Checkpoint(MigrationCategoryState.Abandoned, UnknownCategoryId) };
        var outside = MigrationStartup.UnfinishedRowsOutsideTheCopy(checkpoints, [EndpointSettings]);

        Assert.Multiple(() =>
        {
            Assert.That(MigrationStartup.RequiredCopyIsSettled(checkpoints, [EndpointSettings]), Is.True);
            Assert.DoesNotThrow(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(MigrationCategoryState.Complete)], outside, NewSettings()));
        });
    }

    [TestCase(MigrationCategoryState.Complete)]
    [TestCase(MigrationCategoryState.Abandoned)]
    public void A_finished_row_under_an_id_this_build_does_not_know_holds_nothing(MigrationCategoryState state) =>
        Assert.That(MigrationStartup.UnfinishedRowsOutsideTheCopy([Checkpoint(state, UnknownCategoryId)], [EndpointSettings]), Is.Empty);

    [Test]
    public void An_unfinished_row_known_to_be_optional_holds_nothing() =>
        Assert.That(MigrationStartup.UnfinishedRowsOutsideTheCopy([Checkpoint(MigrationCategoryState.InProgress, MigrationCategoryIds.EventLog)], [EndpointSettings]), Is.Empty);

    [Test]
    public void A_category_that_reported_no_checkpoint_at_all_keeps_the_host_closed()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [], NewSettings()));

        Assert.That(exception.Message, Does.Contain(MigrationCategoryIds.EndpointSettings),
            "an empty result reads as success unless the gate checks what it asked for against what came back");
    }

    [Test]
    public void Of_two_attempted_categories_the_one_that_reported_nothing_is_the_one_named()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete(
            [KnownEndpoints, EndpointSettings],
            [Checkpoint(MigrationCategoryState.Complete, MigrationCategoryIds.KnownEndpoints)],
            NewSettings()));

        Assert.That(exception.Message, Does.Contain($"{MigrationCategoryIds.EndpointSettings} reported no checkpoint at all").And.Not.Contain(MigrationCategoryIds.KnownEndpoints),
            "comparing counts instead of ids would let a two-category run through with one category's fate unknown");
    }

    [Test]
    public void The_refusal_reads_to_its_end_as_a_sentence_and_says_how_to_get_back()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(MigrationCategoryState.Halted)], NewSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain($"--migration-retry {MigrationCategoryIds.EndpointSettings}").And.Contain($"--migration-abandon {MigrationCategoryIds.EndpointSettings}"),
                "a Failed category waits for the operator, so a refusal naming neither command leaves them restarting for ever");
            Assert.That(exception.Message, Does.Not.Contain("the copy resumes from its last committed batch"), "a restart no longer copies a Failed category");
            Assert.That(exception.Message, Does.Contain("both with ServiceControl stopped. Nothing has opened on SQLServer yet")
                .And.EndWith("returns the instance to RavenDB with no loss."),
                "the operator reads the whole message, and the rollback instructions are at the end of it");
        });
    }

    [Test]
    public void Every_failed_required_category_is_named_with_its_skips_and_both_commands()
    {
        var withErrors = Checkpoint(MigrationCategoryState.CompleteWithErrors, MigrationCategoryIds.RecentAndRetriedFailedMessages) with
        {
            CopiedCount = 40,
            SkippedCount = 6,
            SkipReasons = new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.RequiredValueMissing] = 2,
                [MigrationSkipReason.BodyUnreadable] = 3,
                [MigrationSkipReason.BodyMissing] = 1
            }
        };
        var halted = Checkpoint(MigrationCategoryState.Halted, MigrationCategoryIds.MessageRedirects) with { LastError = "TimeoutException at cursor r-7: the target did not answer" };

        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete(
            [MessageRedirects, RecentAndRetriedFailedMessages], [halted, withErrors], NewSettings()));

        using (Assert.EnterMultipleScope())
        {
            foreach (var categoryId in new[] { MigrationCategoryIds.MessageRedirects, MigrationCategoryIds.RecentAndRetriedFailedMessages })
            {
                Assert.That(exception.Message, Does.Contain($"--migration-retry {categoryId} once the cause is fixed").And.Contain($"--migration-abandon {categoryId} to keep what was copied"),
                    $"one start gives every required category its go, so the refusal has to name every Failed one, {categoryId} included");
            }

            Assert.That(exception.Message, Does.Contain($"{MigrationCategoryIds.MessageRedirects} is Failed (Halted)").And.Contain("the target did not answer"));
            Assert.That(exception.Message, Does.Contain($"{MigrationCategoryIds.RecentAndRetriedFailedMessages} is Failed (CompleteWithErrors) after copying 40 and skipping 6"));
            Assert.That(exception.Message, Does.Contain("RequiredValueMissing 2, no retry can fix"), "a row no retry can store is a reason to abandon, and the operator has to be told which");
            Assert.That(exception.Message, Does.Contain("BodyUnreadable 3").And.Not.Contain("BodyUnreadable 3, no retry can fix"), "an unreadable body may read on the next try");
            Assert.That(exception.Message, Does.Contain("BodyMissing 1, no retry can fix"), "a body the source does not hold is missing again on every retry");
        }
    }

    [Test]
    public void A_category_waiting_behind_a_failed_one_is_named_as_waiting()
    {
        var halted = Checkpoint(MigrationCategoryState.Halted, MigrationCategoryIds.KnownEndpoints);
        var blocked = Checkpoint(MigrationCategoryState.Blocked) with { LastError = "Blocked: EndpointSettings must follow KnownEndpoints, which is Halted" };

        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([KnownEndpoints, EndpointSettings], [halted, blocked], NewSettings()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"{MigrationCategoryIds.EndpointSettings} is Blocked").And.Contain("must follow KnownEndpoints"));
            Assert.That(exception.Message, Does.Contain($"--migration-retry {MigrationCategoryIds.KnownEndpoints}"));
            Assert.That(exception.Message, Does.Not.Contain($"--migration-retry {MigrationCategoryIds.EndpointSettings}").And.Not.Contain($"--migration-abandon {MigrationCategoryIds.EndpointSettings}"),
                "a category that never got its go has nothing to retry; it runs once the one ahead of it is dealt with");
        }
    }

    [Test]
    public void A_stalled_category_is_refused_with_the_limit_its_row_records()
    {
        var stalled = Checkpoint(MigrationCategoryState.Halted) with { LastError = MigrationStartup.StallExplanation(MigrationCategoryIds.EndpointSettings) };

        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [stalled], NewSettings()));

        Assert.That(exception.Message, Does.Contain($"committed nothing for {MigrationStartup.ClosedWindowProgress.StallLimit.TotalMinutes:0.#} minutes")
            .And.Contain("looking for a setting to change; run --migration-retry EndpointSettings"),
            "the stall is on the row, and the refusal is where the operator reads it");
    }

    // The last thing said before the cutover is one way, and the only place a skipped total appears at all.
    [Test]
    public void A_failed_category_that_skipped_rows_is_told_a_retry_re_reads_them()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.CompleteWithErrors, copied: 7, skipped: 6, new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.PastRetention] = 3,
                [MigrationSkipReason.RequiredValueMissing] = 2,
                [MigrationSkipReason.BodyUnreadable] = 1
            })],
            logger,
            RunStartedAt,
            NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning), "rows left behind are not an informational matter");
            Assert.That(entry.Message, Does.Contain("6 skipped"), "the total is the number the operator decides on");
            Assert.That(entry.Message, Does.Contain("PastRetention 3").And.Contain("RequiredValueMissing 2").And.Contain("BodyUnreadable 1"), "a total with no reasons cannot be acted on");
            Assert.That(entry.Message, Does.Contain("stay only in the source database").And.Contain($"--migration-retry {MigrationCategoryIds.EndpointSettings} re-reads them"),
                "without this the operator either waits for a copy that is already over or never learns the rows can still come across");
            Assert.That(entry.Message, Does.Contain("No retry can fix the RequiredValueMissing ones"), "the retry brings the unreadable bodies across but leaves these behind again");
        });
    }

    [Test]
    public void A_failed_category_whose_faults_no_retry_can_fix_is_pointed_at_abandon()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.CompleteWithErrors, copied: 7, skipped: 5, new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.PastRetention] = 3,
                [MigrationSkipReason.RequiredValueMissing] = 2
            })],
            logger,
            RunStartedAt,
            NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Does.Contain("no retry can fix them").And.Contain($"--migration-abandon {MigrationCategoryIds.EndpointSettings}"),
                "a retry re-reads the whole category and ends Failed on the same rows");
            Assert.That(entry.Message, Does.Not.Contain("--migration-retry"), "harmless skips are not a reason to retry either");
        });
    }

    [Test]
    public void A_halted_category_whose_faults_no_retry_can_fix_is_told_abandon_gives_up_every_row_not_yet_read()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.Halted, copied: 7, skipped: 120, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.MalformedValue] = 120 })
                with { LastError = "Halted: 120 of 127 rows skipped in this run exceeds the configured threshold of 5% and 100 rows." }],
            logger,
            RunStartedAt,
            NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Does.Contain("stopped before the end").And.Contain("every row not yet read"),
                "a halted category never read the rows after its cursor, so abandon gives up far more than the skipped ones");
            Assert.That(entry.Message, Does.Contain(MigrationSettings.HaltThresholdPercentKey).And.Contain(MigrationSettings.HaltThresholdMinimumKey)
                .And.Contain($"--migration-retry {MigrationCategoryIds.EndpointSettings}"), "raising the threshold and retrying copies every row the faults do not stop");
            Assert.That(entry.Message, Does.Contain("No retry can fix the MalformedValue ones"));
            Assert.That(entry.Message, Does.Not.Contain("gives up the rest"));
        });
    }

    [Test]
    public void A_halted_category_with_faults_a_retry_can_fix_is_also_told_what_abandon_gives_up()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.Halted, copied: 7, skipped: 120, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 120 })],
            logger,
            RunStartedAt,
            NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Does.Contain("every row not yet read").And.Contain($"--migration-retry {MigrationCategoryIds.EndpointSettings}"));
            Assert.That(entry.Message, Does.Not.Contain("No retry can fix"), "an unreadable body may read on the next try");
        });
    }

    [Test]
    public void A_category_that_failed_with_only_harmless_skips_is_still_offered_a_retry()
    {
        var logger = new CapturingLogger();

        var stoppedByAnException = Settled(MigrationCategoryState.Halted, copied: 7, skipped: 3, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.PastRetention] = 3 })
            with
        { LastError = "TimeoutException at cursor r-7: the target did not answer" };

        MigrationStartup.ReportWhatTheCopyLeftBehind([stoppedByAnException], logger, RunStartedAt, NothingMergedOrChanged);

        Assert.That(logger.Entries.Single().Message, Does.Contain($"--migration-retry {MigrationCategoryIds.EndpointSettings} re-reads them").And.Not.Contain("--migration-abandon"),
            "no fault skip stands in the way of a retry, so the exception's cause is what the operator fixes");
    }

    [Test]
    public void A_done_category_with_harmless_skips_is_not_offered_a_retry()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.Complete, copied: 7, skipped: 3, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.PastRetention] = 3 })],
            logger,
            RunStartedAt,
            NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Does.Contain("3 skipped").And.Contain("PastRetention 3"));
            Assert.That(entry.Message, Does.Contain("harmless").And.Contain("stay only in the source database"));
            Assert.That(entry.Message, Does.Not.Contain("--migration-retry"), "a Done category cannot be retried, so offering it sends the operator to a command that refuses");
        });
    }

    [Test]
    public void Every_harmless_skip_reason_has_its_own_explanation_and_no_other_reason_has_one() =>
        Assert.That(MigrationStartup.HarmlessSkipExplanations.Keys, Is.EquivalentTo(Enum.GetValues<MigrationSkipReason>().Where(reason => reason.IsBenign())),
            "the warning for a Done category explains each harmless reason it skipped, so a harmless reason with no explanation would be left out unexplained");

    [Test]
    public void A_done_category_is_told_why_each_of_its_harmless_reasons_is_harmless_and_nothing_about_the_others()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.Complete, copied: 7, skipped: 8, new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.SourceStorageCheck] = 6,
                [MigrationSkipReason.BlankGroupComment] = 2
            })],
            logger,
            RunStartedAt,
            NothingMergedOrChanged);

        var message = logger.Entries.Single().Message;

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain(MigrationStartup.HarmlessSkipExplanations[MigrationSkipReason.SourceStorageCheck]).And.Contain(MigrationStartup.HarmlessSkipExplanations[MigrationSkipReason.BlankGroupComment]));
            Assert.That(message, Does.Not.Contain(MigrationStartup.HarmlessSkipExplanations[MigrationSkipReason.PastRetention]), "a reason the category did not skip for would give the operator a false reason");
        });
    }

    [Test]
    public void The_refusal_marks_a_harmless_reason_so_it_does_not_read_as_rows_a_retry_could_copy()
    {
        var failed = Checkpoint(MigrationCategoryState.CompleteWithErrors, MigrationCategoryIds.CustomChecks) with
        {
            SkippedCount = 7,
            SkipReasons = new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.SourceStorageCheck] = 6,
                [MigrationSkipReason.MalformedValue] = 1
            }
        };

        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([CustomChecks], [failed], NewSettings()));

        Assert.That(exception.Message, Does.Contain("SourceStorageCheck 6, harmless").And.Contain("MalformedValue 1, no retry can fix"), "a retry skips the six harmless rows again");
    }

    [Test]
    public void A_blocked_category_is_left_to_the_refusal_rather_than_reported_as_a_clean_copy()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind([Settled(MigrationCategoryState.Blocked, copied: 0, skipped: 0, null)], logger, RunStartedAt, NothingMergedOrChanged);

        Assert.That(logger.Entries, Is.Empty);
    }

    // Every refused start reports it again, directly above the refusal that calls it Failed.
    [Test]
    public void A_failed_category_that_skipped_nothing_is_not_reported_as_a_clean_copy()
    {
        var logger = new CapturingLogger();

        var stalled = Settled(MigrationCategoryState.Halted, copied: 10, skipped: 0, null) with { LastError = MigrationStartup.StallExplanation(MigrationCategoryIds.EndpointSettings) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([stalled], logger, RunStartedAt, NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Message, Does.Contain($"{MigrationCategoryIds.EndpointSettings} is Failed (Halted) after 10 copied").And.Not.Contain("nothing skipped"));
        });
    }

    [Test]
    public void A_category_that_skipped_nothing_says_so_without_raising_a_warning()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind([Settled(MigrationCategoryState.Complete, copied: 7, skipped: 0, null)], logger, RunStartedAt, NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Information), "a clean copy warning about nothing trains the operator to ignore the warning that matters");
            Assert.That(entry.Message, Does.Contain("nothing skipped"));
        });
    }

    // Restarting a migrated instance used to reprint the whole copy summary in the present tense, so an operator
    // restarting to change a setting read "7 copied" and had no way to tell the copy had not run again.
    [Test]
    public void A_category_an_earlier_run_finished_is_not_reported_as_copied_again()
    {
        var logger = new CapturingLogger();

        var alreadyDone = Settled(MigrationCategoryState.Complete, copied: 7, skipped: 0, null)
            with
        { SettledAt = RunStartedAt.AddMinutes(-5) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([alreadyDone], logger, RunStartedAt, NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Does.Contain("already finished before this start"), "without this the line is indistinguishable from a copy that just ran");
            Assert.That(entry.Message, Does.Contain("This start copied nothing"), "the operator needs to know nothing was written to a target that is already serving");
            Assert.That(entry.Message, Does.Contain("7"), "the historical total is still worth stating, just not as this run's work");
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
        });
    }

    // A category this run actually settled must keep the ordinary wording, or the fix above would silence every report.
    [Test]
    public void A_category_this_run_finished_is_still_reported_as_copied()
    {
        var logger = new CapturingLogger();

        var justDone = Settled(MigrationCategoryState.Complete, copied: 7, skipped: 0, null)
            with
        { SettledAt = RunStartedAt.AddSeconds(2) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([justDone], logger, RunStartedAt, NothingMergedOrChanged);

        Assert.That(logger.Entries.Single().Message, Does.Contain("7 copied").And.Not.Contain("already finished"));
    }

    // A Failed row comes back untouched on every start, so every refused restart reports it again.
    [Test]
    public void A_category_an_earlier_run_left_failed_is_not_reported_as_finished()
    {
        var logger = new CapturingLogger();

        var failedEarlier = Settled(MigrationCategoryState.CompleteWithErrors, copied: 7, skipped: 2, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2 })
            with
        { SettledAt = RunStartedAt.AddMinutes(-5) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([failedEarlier], logger, RunStartedAt, NothingMergedOrChanged);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Does.Not.Contain("already finished"), "the refusal right after this line calls the same category Failed");
            Assert.That(entry.Message, Does.Contain($"--migration-abandon {MigrationCategoryIds.EndpointSettings}").And.Not.Contain("--migration-retry"),
                "the advice has to show on every refused start, not only the first, and no retry can store a row missing a required value");
        });
    }

    [Test]
    public void A_category_that_merged_or_changed_rows_says_how_many_apart_from_already_present_and_warns()
    {
        var logger = new CapturingLogger();
        var done = Settled(MigrationCategoryState.Complete, copied: 15, skipped: 0, null) with { AlreadyPresentCount = 3, SettledAt = RunStartedAt.AddSeconds(2) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([done], logger, RunStartedAt, new Dictionary<string, (long Merged, long Changed)> { [MigrationCategoryIds.EndpointSettings] = (1, 4) });

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Message, Is.EqualTo($"{MigrationCategoryIds.EndpointSettings}: 15 copied (4 changed), 2 already present, 1 merged, nothing skipped"),
                "a merged row is counted as already present, so without taking it out the summary says nothing changed");
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning), "a merged or changed row is one the operator did not expect, and the per-row warnings above name each one");
        });
    }

    [Test]
    public void A_category_that_merged_rows_and_skipped_some_counts_both()
    {
        var logger = new CapturingLogger();
        var failed = Settled(MigrationCategoryState.CompleteWithErrors, copied: 7, skipped: 2, new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2 })
            with
        { AlreadyPresentCount = 5 };

        MigrationStartup.ReportWhatTheCopyLeftBehind([failed], logger, RunStartedAt, new Dictionary<string, (long Merged, long Changed)> { [MigrationCategoryIds.EndpointSettings] = (2, 0) });

        Assert.That(logger.Entries.Single().Message, Does.StartWith($"{MigrationCategoryIds.EndpointSettings}: 7 copied (0 changed), 3 already present, 2 merged, 2 skipped (RequiredValueMissing 2)."));
    }

    [Test]
    public void More_merged_rows_than_rows_already_present_is_a_warning_naming_both_counts()
    {
        var logger = new CapturingLogger();
        var done = Settled(MigrationCategoryState.Complete, copied: 5, skipped: 0, null) with { AlreadyPresentCount = 1, SettledAt = RunStartedAt.AddSeconds(2) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([done], logger, RunStartedAt, new Dictionary<string, (long Merged, long Changed)> { [MigrationCategoryIds.EndpointSettings] = (3, 0) });

        var messages = logger.Entries.Select(entry => entry.Message).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Member($"{MigrationCategoryIds.EndpointSettings}: the target reported 3 merged rows but only 1 rows already present. Its key comparer disagrees with the database about which keys are equal, so the merge lines for this category may name the wrong rows."),
                "every merged row is also already present, so more merges than that is the comparer predicting wrongly");
            Assert.That(messages, Has.Member($"{MigrationCategoryIds.EndpointSettings}: 5 copied (0 changed), 0 already present, 3 merged, nothing skipped"), "never a negative count");
        });
    }

    [Test]
    public void A_failed_category_that_skipped_nothing_still_counts_its_merged_and_changed_rows()
    {
        var logger = new CapturingLogger();
        var stalled = Settled(MigrationCategoryState.Halted, copied: 10, skipped: 0, null) with { AlreadyPresentCount = 3, LastError = MigrationStartup.StallExplanation(MigrationCategoryIds.EndpointSettings) };

        MigrationStartup.ReportWhatTheCopyLeftBehind([stalled], logger, RunStartedAt, new Dictionary<string, (long Merged, long Changed)> { [MigrationCategoryIds.EndpointSettings] = (1, 2) });

        Assert.That(logger.Entries.Single().Message, Does.StartWith($"{MigrationCategoryIds.EndpointSettings} is Failed (Halted) after 10 copied (2 changed), 2 already present and 1 merged."));
    }

    static readonly IReadOnlyDictionary<string, (long Merged, long Changed)> NothingMergedOrChanged = new Dictionary<string, (long Merged, long Changed)>();

    static readonly DateTime RunStartedAt = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    static MigrationCheckpoint Settled(MigrationCategoryState state, long copied, long skipped, IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons) =>
        new(MigrationCategoryIds.EndpointSettings, state, null, copied, skipped, null, skipReasons, null, null, null, null);

    static readonly MigrationCategory EndpointSettings = MigrationCategoryRegistry.Find(MigrationCategoryIds.EndpointSettings)!;
    static readonly MigrationCategory KnownEndpoints = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;
    static readonly MigrationCategory MessageRedirects = MigrationCategoryRegistry.Find(MigrationCategoryIds.MessageRedirects)!;
    static readonly MigrationCategory CustomChecks = MigrationCategoryRegistry.Find(MigrationCategoryIds.CustomChecks)!;
    static readonly MigrationCategory RecentAndRetriedFailedMessages = MigrationCategoryRegistry.Find(MigrationCategoryIds.RecentAndRetriedFailedMessages)!;

    const string UnknownCategoryId = "SomeCategoryFromANewerBuild";

    static MigrationCheckpoint Checkpoint(MigrationCategoryState state, string categoryId = MigrationCategoryIds.EndpointSettings) =>
        new(categoryId, state, null, 0, 0, null, null, null, null, null, null);

    static Settings NewSettings() =>
        new(transportType: "LearningTransport", persisterType: "SQLServer", errorRetentionPeriod: TimeSpan.FromDays(10));
}
