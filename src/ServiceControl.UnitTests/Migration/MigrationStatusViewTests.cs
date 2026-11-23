namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationStatusViewTests
{
    [Test]
    public void A_category_with_no_checkpoint_row_reports_as_not_started_rather_than_missing()
    {
        var view = MigrationStatusView.Build([], []);
        var endpointSettings = view.Single(status => status.Category.Id == "EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Select(status => status.Category.Id), Is.EquivalentTo(MigrationCategoryRegistry.All.Select(category => category.Id)), "every registered category appears exactly once");
            Assert.That(endpointSettings.Selected, Is.True, "a required category is always selected; it has no row because nothing has started it yet");
            Assert.That(endpointSettings.State, Is.EqualTo(MigrationCategoryState.NotStarted));
            Assert.That(endpointSettings.CopiedCount, Is.Zero);
            Assert.That(endpointSettings.LastError, Is.Null);
            Assert.That(endpointSettings.CustomerState, Is.EqualTo("Copying"));
            Assert.That(endpointSettings.NextCommands, Is.Null, "the guided run offers retry or abandon only for a Failed required category");
        }
    }

    static readonly Dictionary<MigrationCategoryState, string> CustomerWords = new()
    {
        [MigrationCategoryState.NotStarted] = "Copying",
        [MigrationCategoryState.InProgress] = "Copying",
        [MigrationCategoryState.Blocked] = "Copying",
        [MigrationCategoryState.Complete] = "Done",
        [MigrationCategoryState.CompleteWithErrors] = "Failed",
        [MigrationCategoryState.Halted] = "Failed",
        [MigrationCategoryState.Abandoned] = "Abandoned"
    };

    static IEnumerable<TestCaseData> StoredStates =>
        from pair in CustomerWords
        select new TestCaseData(pair.Key, pair.Value);

    [Test]
    public void Every_stored_state_has_an_expected_customer_word() =>
        Assert.That(CustomerWords.Keys, Is.EquivalentTo(Enum.GetValues<MigrationCategoryState>()), "a new state needs its customer word decided here");

    [TestCaseSource(nameof(StoredStates))]
    public void Every_stored_state_reads_as_one_of_the_four_customer_states(MigrationCategoryState state, string expected)
    {
        var checkpoint = new MigrationCheckpoint(
            "MessageRedirects", state, "MessageRedirects/9", CopiedCount: 40, SkippedCount: 0, SourceTotal: 100, SkipReasons: null,
            StartedAt: null, LastProgressAt: null, SettledAt: null, LastError: null);

        var redirects = MigrationStatusView.Build([checkpoint], []).Single(status => status.Category.Id == "MessageRedirects");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(redirects.CustomerState, Is.EqualTo(expected));
            Assert.That(redirects.NextCommands, expected switch
            {
                "Failed" => Is.EqualTo("--start-data-migration --retry=MessageRedirects  or  --start-data-migration --abandon=MessageRedirects"),
                _ => Is.Null
            });
        }
    }

    [Test]
    public void A_row_under_an_id_the_registry_does_not_know_is_shown_as_required_after_the_registered_categories()
    {
        var checkpoint = new MigrationCheckpoint(
            "CategoryFromANewerBuild", MigrationCategoryState.Halted, "Items/9", CopiedCount: 4, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
            StartedAt: null, LastProgressAt: null, SettledAt: null, LastError: "TimeoutException at cursor Items/9: the source stopped answering");

        var view = MigrationStatusView.Build([checkpoint], []);
        var unknown = view[^1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view, Has.Count.EqualTo(MigrationCategoryRegistry.All.Count + 1));
            Assert.That(unknown.Category.Id, Is.EqualTo("CategoryFromANewerBuild"));
            Assert.That(unknown.Category.Kind, Is.EqualTo(MigrationCategoryKind.Required));
            Assert.That(unknown.CustomerState, Is.EqualTo("Failed"));
            Assert.That(unknown.NextCommands, Is.EqualTo("--start-data-migration with the build that created CategoryFromANewerBuild, which this build does not know"), "the arguments refuse an id this build does not know");
        }
    }

    [Test]
    public void An_optional_category_is_offered_nothing_and_is_unselected_when_nothing_selects_it()
    {
        var failedArchive = new MigrationCheckpoint(
            "ArchivedAndResolvedFailedMessages", MigrationCategoryState.Halted, "FailedMessages/9",
            CopiedCount: 240, SkippedCount: 10, SourceTotal: null, SkipReasons: new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 10 },
            StartedAt: new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc),
            LastProgressAt: new DateTime(2026, 1, 1, 2, 4, 0, DateTimeKind.Utc),
            SettledAt: new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc),
            LastError: "BodyUnreadable at cursor FailedMessages/9: the body store stopped answering",
            AlreadyPresentCount: 7);
        var copyingEventLog = new MigrationCheckpoint(
            "EventLog", MigrationCategoryState.InProgress, null, CopiedCount: 0, SkippedCount: 0, SourceTotal: null, SkipReasons: null,
            StartedAt: null, LastProgressAt: null, SettledAt: null, LastError: "TimeoutException at cursor EventLogItems/40: RavenDB stopped answering");

        var view = MigrationStatusView.Build([failedArchive, copyingEventLog], []);
        var archive = view.Single(status => status.Category.Id == "ArchivedAndResolvedFailedMessages");
        var eventLog = view.Single(status => status.Category.Id == "EventLog");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(archive.State, Is.EqualTo(MigrationCategoryState.Halted));
            Assert.That((archive.CopiedCount, archive.SkippedCount, archive.AlreadyPresentCount), Is.EqualTo((240L, 10L, 7L)));
            Assert.That(archive.StartedAt, Is.EqualTo(new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc)));
            Assert.That(archive.LastProgressAt, Is.EqualTo(new DateTime(2026, 1, 1, 2, 4, 0, DateTimeKind.Utc)));
            Assert.That(archive.SettledAt, Is.EqualTo(new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc)));
            Assert.That(archive.LastError, Is.EqualTo("BodyUnreadable at cursor FailedMessages/9: the body store stopped answering"));
            Assert.That(archive.Selected, Is.False, "this build copies no optional category, so status never selects one");
            Assert.That(archive.CustomerState, Is.EqualTo("Failed"));
            Assert.That(archive.NextCommands, Is.Null, "the guided run copies only the required categories, so it offers no command for an optional one");
            Assert.That(eventLog.CustomerState, Is.EqualTo("Copying"), "an error does not make an optional category Failed");
            Assert.That(eventLog.NextCommands, Is.Null, "the guided run offers retry or abandon only for a Failed required category");
        }
    }
}
