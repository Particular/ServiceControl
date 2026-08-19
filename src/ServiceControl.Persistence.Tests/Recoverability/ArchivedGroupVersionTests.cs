namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;

[TestFixture]
class ArchivedGroupVersionTests : PersistenceTestBase
{
    const string Classifier = "Exception Type and Stack Trace";

    static readonly DateTime Oldest = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Middle = new(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Newest = new(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Version_changes_when_group_counts_move_but_the_total_and_the_span_hold()
    {
        var stays = NewGroup("Shipping");
        var goes = NewGroup("Billing");

        var oldest = InGroup(stays, Oldest);
        var newest = InGroup(stays, Newest);
        var middle = InGroup(goes, Middle);

        await Insert(oldest, newest, middle);
        await Archive(oldest, newest, middle);

        var before = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        // One message leaves the archived set and another joins it in the same span, so the total
        // stays at three and neither the earliest nor the latest failure moves. Only the per group
        // counts change, and those are what the body reports.
        var replacement = InGroup(stays, Middle);
        await Insert(replacement);
        await Archive(replacement);
        _ = await FailedMessageLifecycleStore.UnArchiveMessages([middle.UniqueMessageIdString]);
        await CompleteDatabaseOperation();

        var after = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.Results, Has.Count.EqualTo(2), "two archived groups to start with");
            Assert.That(after.Results, Has.Count.EqualTo(1), "and one afterwards, so the body definitely changed");
            Assert.That(after.Results.Single().Count, Is.EqualTo(3), "carrying all three archived messages");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too, or a revalidating client keeps a group that is gone");
        }
    }

    [Test]
    public async Task Version_changes_when_a_group_gains_a_message()
    {
        var group = NewGroup("Shipping");
        var first = InGroup(group, Oldest);

        await Insert(first);
        await Archive(first);

        var before = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        var second = InGroup(group, Newest);
        await Insert(second);
        await Archive(second);

        var after = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False);
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        var group = NewGroup("Shipping");
        var failure = InGroup(group, Oldest);

        await Insert(failure);
        await Archive(failure);

        var first = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);
        var second = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        Assert.That(second.QueryStats.Version.Matches(first.QueryStats.Version), Is.True);
    }

    [Test]
    public async Task A_classifier_with_nothing_archived_still_reports_a_version()
    {
        var result = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.QueryStats.Version.HasValue, Is.True);
        }
    }

    static FailedMessage.FailureGroup NewGroup(string title) =>
        new() { Id = Guid.NewGuid().ToString(), Title = title, Type = Classifier };

    static IngestedFailure InGroup(FailedMessage.FailureGroup group, DateTime failedAt) =>
        new()
        {
            Groups = [group],
            AttemptedAt = failedAt,
            TimeOfFailure = failedAt,
            TimeSent = failedAt.AddMinutes(-1)
        };

    async Task Archive(params IngestedFailure[] failures)
    {
        foreach (var failure in failures)
        {
            await FailedMessageLifecycleStore.MarkAsArchived(failure.UniqueMessageIdString);
        }

        await CompleteDatabaseOperation();
    }

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
