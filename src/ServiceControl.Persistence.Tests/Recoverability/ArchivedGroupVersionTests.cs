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
        var shipping = NewGroup("Shipping");
        var billing = NewGroup("Billing");

        var oldest = InGroup(shipping, Oldest);
        var middle = InGroup(shipping, Middle);
        var newest = InGroup(billing, Newest);

        await Insert(oldest, middle, newest);
        await Archive(oldest, middle, newest);

        var before = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        // The archived set keeps two groups, three messages, and the same earliest and latest failure.
        // All that moves is how the three are split between the groups, from two and one to one and two.
        var replacement = InGroup(billing, Middle);
        await Insert(replacement);
        await Archive(replacement);
        _ = await FailedMessageLifecycleStore.UnArchiveMessages([middle.UniqueMessageIdString]);
        await CompleteDatabaseOperation();

        var after = await GroupsStore.GetArchivedGroupsByClassifier(Classifier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.Results, Has.Count.EqualTo(2), "two archived groups to start with");
            Assert.That(after.Results, Has.Count.EqualTo(2), "and still two afterwards");
            Assert.That(after.Results.Sum(group => group.Count), Is.EqualTo(3), "still three archived messages between them");
            Assert.That(after.Results.Max(group => group.Last), Is.EqualTo(before.Results.Max(group => group.Last)), "and the latest failure has not moved");
            Assert.That(before.Results.Single(group => group.Title == "Shipping").Count, Is.EqualTo(2), "Shipping held two of them");
            Assert.That(after.Results.Single(group => group.Title == "Shipping").Count, Is.EqualTo(1), "and now holds one, so the split between the groups moved");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body reports a different count per group, so the validator cannot stay put");
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

        VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version,
            "the archived group gained a message, so its validator cannot stay put");
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

        VersionAssert.Held(first.QueryStats.Version, second.QueryStats.Version,
            "nothing changed, so the validator has to stay put or conditional GET never pays off");
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
