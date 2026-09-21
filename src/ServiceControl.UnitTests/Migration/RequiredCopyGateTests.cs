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
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    public void A_finished_required_category_lets_the_host_open(MigrationCategoryState state) =>
        Assert.DoesNotThrow(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(state)], null, NewSettings()));

    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.NotStarted)]
    // Blocked happens because EndpointSettings must follow KnownEndpoints.
    [TestCase(MigrationCategoryState.Blocked)]
    public void A_required_category_that_is_not_finished_keeps_the_host_closed(MigrationCategoryState state)
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(state)], null, NewSettings()));

        Assert.That(exception.Message, Does.Contain(MigrationCategoryIds.EndpointSettings).And.Contain(state.ToString()));
    }

    [Test]
    public void A_category_that_reported_no_checkpoint_at_all_keeps_the_host_closed()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [], null, NewSettings()));

        Assert.That(exception.Message, Does.Contain(MigrationCategoryIds.EndpointSettings),
            "an empty result reads as success unless the gate checks what it asked for against what came back");
    }

    [Test]
    public void Of_two_attempted_categories_the_one_that_reported_nothing_is_the_one_named()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete(
            [KnownEndpoints, EndpointSettings],
            [Checkpoint(MigrationCategoryState.Complete, MigrationCategoryIds.KnownEndpoints)],
            null,
            NewSettings()));

        Assert.That(exception.Message, Does.Contain($"{MigrationCategoryIds.EndpointSettings} reported no checkpoint at all").And.Not.Contain(MigrationCategoryIds.KnownEndpoints),
            "comparing counts instead of ids would let a two-category run through with one category's fate unknown");
    }

    [Test]
    public void The_refusal_reads_to_its_end_as_a_sentence_and_says_how_to_get_back()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete([EndpointSettings], [Checkpoint(MigrationCategoryState.Halted)], null, NewSettings()));

        Assert.That(exception.Message, Does.Contain("the copy resumes from its last committed batch. Nothing has opened on SQLServer yet")
            .And.Contain($"setting {MigrationSettings.EnabledKey}=false and pointing PersistenceType back at RavenDB"),
            "the operator reads the whole message, and the rollback instructions are at the end of it");
    }

    [Test]
    public void A_stall_is_reported_with_the_limit_that_was_actually_measured()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete(
            [EndpointSettings], [Checkpoint(MigrationCategoryState.InProgress)], MigrationCategoryIds.EndpointSettings, NewSettings()));

        Assert.That(exception.Message, Does.Contain($"committed nothing for {MigrationStartup.ClosedWindowProgress.StallLimit.TotalMinutes:0.#} minutes")
            .And.Contain("looking for a setting to change. Nothing has opened on SQLServer yet"),
            "prose spelling the limit out goes stale the moment the constant moves");
    }

    [Test]
    public void A_stall_on_a_category_that_finished_is_not_blamed_for_what_is_outstanding()
    {
        var exception = Assert.Throws<Exception>(() => MigrationStartup.RefuseIfAnyCategoryDidNotComplete(
            [KnownEndpoints, EndpointSettings],
            [Checkpoint(MigrationCategoryState.Complete, MigrationCategoryIds.KnownEndpoints), Checkpoint(MigrationCategoryState.Halted)],
            MigrationCategoryIds.KnownEndpoints,
            NewSettings()));

        Assert.That(exception.Message, Does.Not.Contain("committed nothing for").And.Contain("Fix the cause and restart"),
            "blaming a stall for a halt it had nothing to do with sends the operator after the wrong thing");
    }

    // The last thing said before the cutover is one way, and the only place a skipped total appears at all.
    [Test]
    public void A_category_that_skipped_rows_is_reported_as_rows_nothing_will_come_back_for()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind(
            [Settled(MigrationCategoryState.CompleteWithErrors, copied: 7, skipped: 5, new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.EndpointNotKnown] = 3,
                [MigrationSkipReason.RequiredValueMissing] = 2
            })],
            logger,
            RunStartedAt);

        var entry = logger.Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning), "rows that are never coming back are not an informational matter");
            Assert.That(entry.Message, Does.Contain("5 skipped"), "the total is the number the operator decides on");
            Assert.That(entry.Message, Does.Contain("EndpointNotKnown 3").And.Contain("RequiredValueMissing 2"), "a total with no reasons cannot be acted on");
            Assert.That(entry.Message, Does.Contain("no later run will fetch them"), "without this the operator waits for a copy that is already over");
        });
    }

    [Test]
    public void A_category_that_skipped_nothing_says_so_without_raising_a_warning()
    {
        var logger = new CapturingLogger();

        MigrationStartup.ReportWhatTheCopyLeftBehind([Settled(MigrationCategoryState.Complete, copied: 7, skipped: 0, null)], logger, RunStartedAt);

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

        MigrationStartup.ReportWhatTheCopyLeftBehind([alreadyDone], logger, RunStartedAt);

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

        MigrationStartup.ReportWhatTheCopyLeftBehind([justDone], logger, RunStartedAt);

        Assert.That(logger.Entries.Single().Message, Does.Contain("7 copied").And.Not.Contain("already finished"));
    }

    static readonly DateTime RunStartedAt = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    static MigrationCheckpoint Settled(MigrationCategoryState state, long copied, long skipped, IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons) =>
        new(MigrationCategoryIds.EndpointSettings, state, null, copied, skipped, null, skipReasons, null, null, null, null);

    static readonly MigrationCategory EndpointSettings = MigrationCategoryRegistry.Find(MigrationCategoryIds.EndpointSettings)!;
    static readonly MigrationCategory KnownEndpoints = MigrationCategoryRegistry.Find(MigrationCategoryIds.KnownEndpoints)!;

    static MigrationCheckpoint Checkpoint(MigrationCategoryState state, string categoryId = MigrationCategoryIds.EndpointSettings) =>
        new(categoryId, state, null, 0, 0, null, null, null, null, null, null);

    static Settings NewSettings() =>
        new(transportType: "LearningTransport", persisterType: "SQLServer", errorRetentionPeriod: TimeSpan.FromDays(10));
}
