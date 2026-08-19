namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class FailureGroupVersionTests : PersistenceTestBase
{
    const string Classifier = "Exception Type and Stack Trace";

    static readonly DateTime Oldest = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Middle = new(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Newest = new(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Version_changes_when_a_group_loses_a_message_that_is_neither_its_oldest_nor_its_newest()
    {
        var group = NewGroup();
        var middle = InGroup(group, Middle);

        await Insert(InGroup(group, Oldest), middle, InGroup(group, Newest));

        var before = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        // Archiving takes the message out of the unresolved group without removing the group, and
        // the one chosen is neither the earliest nor the latest, so First and Last both stay put.
        // Count is the only thing that moves, and Count is what the body reports.
        await FailedMessageLifecycleStore.MarkAsArchived(middle.UniqueMessageIdString);
        await CompleteDatabaseOperation();

        var after = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(before.Results.Count, Is.EqualTo(3), "three messages to start with");
            Assert.That(after.Results.Count, Is.EqualTo(2), "and the body now reports two");
            Assert.That(after.Results.First, Is.EqualTo(before.Results.First), "the earliest failure is unchanged");
            Assert.That(after.Results.Last, Is.EqualTo(before.Results.Last), "and so is the latest");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too, or a revalidating client is served a stale count");
        });
    }

    [Test]
    public async Task Version_changes_when_a_group_gains_a_message()
    {
        var group = NewGroup();

        await Insert(InGroup(group, Oldest));

        var before = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        await Insert(InGroup(group, Newest));

        var after = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version,
            "the group gained a message, so its validator cannot stay put");
    }

    [Test]
    public async Task Version_changes_when_the_span_of_a_group_moves_but_its_count_does_not()
    {
        var group = NewGroup();
        var oldest = InGroup(group, Oldest);

        await Insert(oldest, InGroup(group, Middle), InGroup(group, Newest));

        var before = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        // The count lands exactly where it started, so this is the case that proves First and Last
        // are named. A version built from the count alone passes every other case in this fixture.
        await FailedMessageLifecycleStore.MarkAsArchived(oldest.UniqueMessageIdString);
        await Insert(InGroup(group, Newest.AddHours(4)));

        var after = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(after.Results.Count, Is.EqualTo(before.Results.Count), "still three messages");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "a different span of failures is being reported under the same count");
        });
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        var group = NewGroup();

        await Insert(InGroup(group, Oldest));

        var first = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);
        var second = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);

        VersionAssert.Held(first.QueryStats.Version, second.QueryStats.Version,
            "nothing changed, so the validator has to stay put or conditional GET never pays off");
    }

    [Test]
    public async Task The_errors_in_a_group_report_a_version_that_moves_with_them()
    {
        var group = NewGroup();
        var middle = InGroup(group, Middle);

        await Insert(InGroup(group, Oldest), middle, InGroup(group, Newest));

        var before = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo());

        await FailedMessageLifecycleStore.MarkAsArchived(middle.UniqueMessageIdString);
        await CompleteDatabaseOperation();

        var after = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.Results, Has.Count.EqualTo(3), "three errors to start with");
            Assert.That(after.Results, Has.Count.EqualTo(2), "and the body now reports two");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the page lost a row, so the validator cannot stay put");
        }
    }

    [Test]
    public async Task The_error_count_of_a_group_reports_a_version_that_moves_with_it()
    {
        var group = NewGroup();
        var middle = InGroup(group, Middle);

        await Insert(InGroup(group, Oldest), middle, InGroup(group, Newest));

        var before = await GroupsStore.GetGroupErrorsCount(group.Id, "unresolved", null);

        await FailedMessageLifecycleStore.MarkAsArchived(middle.UniqueMessageIdString);
        await CompleteDatabaseOperation();

        var after = await GroupsStore.GetGroupErrorsCount(group.Id, "unresolved", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.TotalCount, Is.EqualTo(3));
            Assert.That(after.TotalCount, Is.EqualTo(2), "the count the body reports has changed");
            Assert.That(before.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.Version.Matches(before.Version), Is.False);
        }
    }

    [Test]
    public async Task An_archived_group_reports_a_version_that_moves_with_it()
    {
        var group = NewGroup();
        var oldest = InGroup(group, Oldest);
        var middle = InGroup(group, Middle);

        await Insert(oldest, middle, InGroup(group, Newest));
        await FailedMessageLifecycleStore.MarkAsArchived(oldest.UniqueMessageIdString);
        await FailedMessageLifecycleStore.MarkAsArchived(middle.UniqueMessageIdString);
        await CompleteDatabaseOperation();

        var before = await GroupsStore.GetArchivedGroup(group.Id, null, null);

        _ = await FailedMessageLifecycleStore.UnArchiveMessages([middle.UniqueMessageIdString]);
        await CompleteDatabaseOperation();

        var after = await GroupsStore.GetArchivedGroup(group.Id, null, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.Results.Count, Is.EqualTo(2), "two archived errors to start with");
            Assert.That(after.Results.Count, Is.EqualTo(1), "and the body now reports one");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False);
        }
    }

    [Test]
    public async Task A_group_that_does_not_exist_still_reports_a_version()
    {
        var result = await GroupsStore.GetUnresolvedGroup("no-such-group", null, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Null);
            Assert.That(result.QueryStats.Version.HasValue, Is.True);
        });
    }

    static FailedMessage.FailureGroup NewGroup() =>
        new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = Classifier };

    static IngestedFailure InGroup(FailedMessage.FailureGroup group, DateTime failedAt) =>
        new()
        {
            Groups = [group],
            AttemptedAt = failedAt,
            TimeOfFailure = failedAt,
            TimeSent = failedAt.AddMinutes(-1)
        };

    async Task Insert(params IngestedFailure[] failures)
    {
        var messages = Array.ConvertAll(failures, failure => failure.ToFailedMessage());

        foreach (var message in messages)
        {
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);
        }

        await PersistenceTestsContext.InsertFailedMessages(messages);
        await CompleteDatabaseOperation();
    }
}
