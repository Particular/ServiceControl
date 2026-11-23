#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationCheckpointRulesTests
{
    static readonly DateTime StartedTwoDaysAgo = new(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);

    static MigrationCheckpoint Checkpoint(string categoryId, MigrationCategoryState state, DateTime? startedAt = null, string? lastError = null) =>
        new(
            CategoryId: categoryId,
            State: state,
            Cursor: null,
            CopiedCount: 0,
            SkippedCount: 0,
            SourceTotal: null,
            SkipReasons: null,
            StartedAt: startedAt,
            LastProgressAt: null,
            SettledAt: null,
            LastError: lastError);

    [TestCase(MigrationCategoryState.Complete)]
    [TestCase(MigrationCategoryState.Abandoned)]
    public void A_finished_category_is_not_outstanding(MigrationCategoryState state) =>
        Assert.That(Checkpoint(MigrationCategoryIds.KnownEndpoints, state).IsOutstanding(MigrationCategoryKind.Required, selected: true), Is.False);

    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    public void A_failed_category_is_outstanding(MigrationCategoryState state) =>
        Assert.That(Checkpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, state, startedAt: StartedTwoDaysAgo).IsOutstanding(MigrationCategoryKind.Optional, selected: true), Is.True,
            "a Failed category waits for the operator, so it is not finished");

    [Test]
    public void An_optional_category_turned_off_before_it_started_is_not_outstanding() =>
        Assert.That(Checkpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryState.NotStarted, lastError: "RavenDB unreachable").IsOutstanding(MigrationCategoryKind.Optional, selected: false), Is.False,
            "a window of 0 turns an optional category off until it starts, so its never-started row is not work left to do");

    [Test]
    public void An_optional_category_in_progress_is_outstanding_with_its_window_at_zero() =>
        Assert.That(Checkpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryState.InProgress).IsOutstanding(MigrationCategoryKind.Optional, selected: false), Is.True,
            "only a NotStarted row can be turned off by a window of 0, so a row in any other unfinished state is still work left to do");

    [Test]
    public void An_optional_category_selected_before_it_started_is_outstanding() =>
        Assert.That(Checkpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryState.NotStarted).IsOutstanding(MigrationCategoryKind.Optional, selected: true), Is.True,
            "a window above 0 selects the category, so its never-started row is work the copy still has to do");

    [Test]
    public void An_optional_category_that_has_started_is_outstanding_with_its_window_at_zero() =>
        Assert.That(Checkpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryState.NotStarted, startedAt: StartedTwoDaysAgo).IsOutstanding(MigrationCategoryKind.Optional, selected: false), Is.True,
            "a retried row keeps StartedAt, and what it copied before is in the target, so only abandoning ends it");

    [Test]
    public void A_required_category_that_never_started_is_outstanding_whatever_the_selection() =>
        Assert.That(Checkpoint(MigrationCategoryIds.EndpointSettings, MigrationCategoryState.NotStarted).IsOutstanding(MigrationCategoryKind.Required, selected: false), Is.True,
            "the windows select optional categories only, and a run seeds a NotStarted row for every required category before copying the first");

    [Test]
    public void A_category_with_no_row_cannot_be_retried() =>
        Assert.That(((MigrationCheckpoint?)null).CanBeRetried(), Is.False,
            "with no row there is nothing to reset, so status must not offer a retry");
}
