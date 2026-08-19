namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;

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

        Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False);
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

        Assert.That(second.QueryStats.Version.Matches(first.QueryStats.Version), Is.True);
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
