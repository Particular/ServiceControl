namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

class GroupsDataStoreTests : PersistenceTestBase
{
    const string Classifier = "Exception Type and Stack Trace";
    const string OtherClassifier = "Message Type";

    static readonly DateTime Noon = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Aggregates_the_messages_of_a_group()
    {
        var group = NewGroup("OrderPlaced");

        await Insert(
            InGroup(group, failedAt: Noon),
            InGroup(group, failedAt: Noon.AddHours(2)));

        var view = (await GroupsStore.GetUnresolvedGroupsByClassifier(Classifier, null)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(group.Id));
            Assert.That(view.Title, Is.EqualTo("OrderPlaced"));
            Assert.That(view.Type, Is.EqualTo(Classifier));
            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(view.First, Is.EqualTo(Noon));
            Assert.That(view.Last, Is.EqualTo(Noon.AddHours(2)));
        }
    }

    [Test]
    public async Task Returns_only_the_groups_of_the_requested_classifier()
    {
        var requested = NewGroup("OrderPlaced");
        var other = NewGroup("OrderPlaced", OtherClassifier);

        await Insert(InGroup(requested), InGroup(other));

        var groups = await GroupsStore.GetUnresolvedGroupsByClassifier(Classifier, null);

        Assert.That(groups.Select(group => group.Id), Is.EqualTo(new[] { requested.Id }));
    }

    [Test]
    public async Task Narrows_the_groups_to_the_classifier_filter()
    {
        var matching = NewGroup("OrderPlaced");
        var other = NewGroup("OrderCancelled");

        await Insert(InGroup(matching), InGroup(other));

        var groups = await GroupsStore.GetUnresolvedGroupsByClassifier(Classifier, "OrderPlaced");

        Assert.That(groups.Select(group => group.Id), Is.EqualTo(new[] { matching.Id }));
    }

    [Test]
    public async Task Counts_only_unresolved_messages()
    {
        var group = NewGroup("OrderPlaced");

        await Insert(
            InGroup(group).ToFailedMessage(),
            InGroup(group).ToFailedMessage(FailedMessageStatus.Archived),
            InGroup(group).ToFailedMessage(FailedMessageStatus.Resolved));

        var view = (await GroupsStore.GetUnresolvedGroupsByClassifier(Classifier, null)).Single();

        Assert.That(view.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Returns_archived_groups_separately()
    {
        var group = NewGroup("OrderPlaced");

        await Insert(
            InGroup(group).ToFailedMessage(),
            InGroup(group).ToFailedMessage(FailedMessageStatus.Archived));

        var view = (await GroupsStore.GetArchivedGroupsByClassifier(Classifier)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(group.Id));
            Assert.That(view.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Orders_groups_by_the_most_recent_failure()
    {
        var oldest = NewGroup("Oldest");
        var newest = NewGroup("Newest");
        var middle = NewGroup("Middle");

        await Insert(
            InGroup(oldest, failedAt: Noon),
            InGroup(newest, failedAt: Noon.AddHours(4)),
            InGroup(middle, failedAt: Noon.AddHours(2)));

        var groups = await GroupsStore.GetUnresolvedGroupsByClassifier(Classifier, null);

        Assert.That(groups.Select(group => group.Title), Is.EqualTo(new[] { "Newest", "Middle", "Oldest" }));
    }

    [Test]
    public async Task Returns_a_single_group_by_id()
    {
        var requested = NewGroup("OrderPlaced");

        await Insert(InGroup(requested), InGroup(NewGroup("OrderCancelled")));

        var result = await GroupsStore.GetUnresolvedGroup(requested.Id, null, null);

        var view = result.Results;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(requested.Id));
            Assert.That(view.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Returns_no_group_for_an_unknown_id()
    {
        await Insert(InGroup(NewGroup("OrderPlaced")));

        var result = await GroupsStore.GetUnresolvedGroup(Guid.NewGuid().ToString(), null, null);

        Assert.That(result.Results, Is.Null);
    }

    [Test]
    public async Task Returns_an_archived_group_view_by_id()
    {
        var group = NewGroup("OrderPlaced");

        await Insert(InGroup(group).ToFailedMessage(FailedMessageStatus.Archived));

        var result = await GroupsStore.GetArchivedGroup(group.Id, null, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Id, Is.EqualTo(group.Id));
            Assert.That(result.Results.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Returns_no_archived_group_view_for_an_unknown_id()
    {
        await Insert(InGroup(NewGroup("OrderPlaced")).ToFailedMessage(FailedMessageStatus.Archived));

        var result = await GroupsStore.GetArchivedGroup(Guid.NewGuid().ToString(), null, null);

        Assert.That(result.Results, Is.Null);
    }

    [Test]
    public async Task Returns_the_errors_of_a_group()
    {
        var group = NewGroup("OrderPlaced");
        var inGroup = InGroup(group);
        var outsideGroup = InGroup(NewGroup("OrderCancelled"));

        await Insert(inGroup, outsideGroup);

        var result = await GroupsStore.GetGroupErrors(group.Id, null, null, new SortInfo(), new PagingInfo());

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inGroup.UniqueMessageIdString }));
    }

    [Test]
    public async Task Pages_the_errors_of_a_group()
    {
        var group = NewGroup("OrderPlaced");
        var newest = InGroup(group, sentAt: Noon.AddHours(2));
        var oldest = InGroup(group, sentAt: Noon);

        await Insert(newest, oldest);

        var result = await GroupsStore.GetGroupErrors(group.Id, null, null, new SortInfo("time_sent", "desc"), new PagingInfo(page: 1, pageSize: 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { newest.UniqueMessageIdString }));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Returns_the_errors_of_a_group_by_status()
    {
        var group = NewGroup("OrderPlaced");
        var unresolved = InGroup(group);
        var archived = InGroup(group);

        await Insert(unresolved.ToFailedMessage(), archived.ToFailedMessage(FailedMessageStatus.Archived));

        var result = await GroupsStore.GetGroupErrors(group.Id, "archived", null, new SortInfo(), new PagingInfo());

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { archived.UniqueMessageIdString }));
    }

    [Test]
    public async Task Counts_the_errors_of_a_group()
    {
        var group = NewGroup("OrderPlaced");

        await Insert(InGroup(group), InGroup(group), InGroup(NewGroup("OrderCancelled")));

        var stats = await GroupsStore.GetGroupErrorsCount(group.Id, null, null);

        Assert.That(stats.TotalCount, Is.EqualTo(2));
    }

    static FailedMessage.FailureGroup NewGroup(string title, string type = Classifier) =>
        new() { Id = Guid.NewGuid().ToString(), Title = title, Type = type };

    static IngestedFailure InGroup(FailedMessage.FailureGroup group, DateTime? failedAt = null, DateTime? sentAt = null) =>
        new()
        {
            Groups = [group],
            AttemptedAt = failedAt ?? Noon,
            TimeOfFailure = failedAt ?? Noon,
            TimeSent = sentAt ?? Noon.AddMinutes(-1)
        };

    Task Insert(params IngestedFailure[] failures) =>
        Insert([.. failures.Select(failure => failure.ToFailedMessage())]);

    async Task Insert(params FailedMessage[] messages)
    {
        foreach (var message in messages)
        {
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);
        }

        await PersistenceTestsContext.InsertFailedMessages(messages);
        await CompleteDatabaseOperation();
    }
}
