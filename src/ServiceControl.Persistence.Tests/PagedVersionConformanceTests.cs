namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contracts.CustomChecks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class PagedVersionConformanceTests : IngestionTestBase
{
    const string ExceptionClassifier = "Exception Type and Stack Trace";
    const string MessageTypeClassifier = "Message Type";

    static readonly DateTime Oldest = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Middle = new(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Newest = new(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc);
    static readonly DateTime ReportedAt = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    static IEnumerable<Scenario> Scenarios() =>
    [
        new("custom checks, two pages",
            "page one and page two of the custom checks list render different checks",
            fixture => fixture.CustomChecksTwoPages()),
        new("custom checks, two status filters",
            "the failing checks and the passing checks are different lists",
            fixture => fixture.CustomChecksTwoStatuses()),
        new("queue addresses, two pages",
            "page one and page two of the queue address list render different addresses",
            fixture => fixture.QueueAddressesTwoPages()),
        new("the errors in a group, two pages",
            "page one and page two of a group's failures render different messages",
            fixture => fixture.GroupErrorsTwoPages()),
        new("the error count of a group, two status filters",
            "the unresolved count and the archived count are different numbers, and the count is the whole response",
            fixture => fixture.GroupErrorCountTwoStatuses()),
        new("archived groups, two classifiers",
            "grouping the archive by exception type and by message type produces different groups",
            fixture => fixture.ArchivedGroupsTwoClassifiers()),
        new("the messages view, two pages",
            "page one and page two of the messages list render different messages",
            fixture => fixture.MessagesViewTwoPages()),
        new("the error list, two status filters that both match nothing",
            "two filters that happen to be empty today are still two different questions",
            fixture => fixture.ErrorsTwoEmptyStatuses()),

        // The cases above compare queries whose rows differ, so the row terms alone tell them apart and
        // they would pass even if a store named none of its filters. These compare two queries that both
        // return nothing, where there are no rows to name and only the query terms are left to do it.
        new("custom checks, two status filters that both match nothing",
            "asking for the failing checks and the passing checks of an empty store are still two questions",
            fixture => fixture.CustomChecksTwoEmptyStatuses()),
        new("custom checks, two pages past the end",
            "two pages beyond the last are two questions, and the paging links differ",
            fixture => fixture.CustomChecksTwoEmptyPages()),
        new("queue addresses, two pages past the end",
            "two pages beyond the last are two questions",
            fixture => fixture.QueueAddressesTwoEmptyPages()),
        new("the errors in a group, two pages past the end",
            "two pages beyond the last are two questions",
            fixture => fixture.GroupErrorsTwoEmptyPages()),
        new("the error count of a group, two status filters that both count nothing",
            "two counts that are both zero still answer different questions",
            fixture => fixture.GroupErrorCountTwoEmptyStatuses()),
        new("archived groups, two classifiers with nothing archived",
            "two classifiers that group nothing are still two questions",
            fixture => fixture.ArchivedGroupsTwoEmptyClassifiers()),
        new("the messages view, two pages past the end",
            "two pages beyond the last are two questions",
            fixture => fixture.MessagesViewTwoEmptyPages())
    ];

    [Test]
    [TestCaseSource(nameof(Scenarios))]
    public async Task Two_queries_of_one_store_do_not_share_a_version(Scenario scenario)
    {
        var queried = await scenario.Run(this);

        // Proves the first query's own version was standing still across the two reads. Without it a
        // shared version below could be waved away as the store legitimately moving between requests.
        VersionAssert.Held(queried.First, queried.FirstAgain,
            "the first query's version moved between two reads of unchanged data, so this scenario cannot judge anything");

        VersionAssert.Distinct(queried.First, queried.Second, scenario.Because);
    }

    async Task<Queried> CustomChecksTwoPages()
    {
        await ReportCheck("Disk space");
        await ReportCheck("Queue length");
        await ReportCheck("Certificate expiry");

        var firstPage = await CustomChecks.GetStats(new PagingInfo(page: 1, pageSize: 2));
        var firstPageAgain = await CustomChecks.GetStats(new PagingInfo(page: 1, pageSize: 2));
        var secondPage = await CustomChecks.GetStats(new PagingInfo(page: 2, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPage.Results, Has.Count.EqualTo(2), "two checks on the first page");
            Assert.That(secondPage.Results, Has.Count.EqualTo(1), "and the third on the second, so the bodies differ");
        }

        return new(firstPage.QueryStats.Version, firstPageAgain.QueryStats.Version, secondPage.QueryStats.Version);
    }

    async Task<Queried> CustomChecksTwoStatuses()
    {
        await ReportCheck("Disk space", hasFailed: true);
        await ReportCheck("Queue length", hasFailed: false);

        var failing = await CustomChecks.GetStats(new PagingInfo(), "fail");
        var failingAgain = await CustomChecks.GetStats(new PagingInfo(), "fail");
        var passing = await CustomChecks.GetStats(new PagingInfo(), "pass");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failing.Results, Has.Count.EqualTo(1), "one failing check");
            Assert.That(passing.Results, Has.Count.EqualTo(1), "one passing check");
            Assert.That(passing.Results[0].Id, Is.Not.EqualTo(failing.Results[0].Id), "and they are not the same check");
        }

        return new(failing.QueryStats.Version, failingAgain.QueryStats.Version, passing.QueryStats.Version);
    }

    async Task<Queried> QueueAddressesTwoPages()
    {
        await Ingest(Failure("Shipping@machine1"), Failure("Billing@machine1"), Failure("Sales@machine1"));
        await CompleteDatabaseOperation();

        var firstPage = await QueueAddressStore.GetAddresses(new PagingInfo(page: 1, pageSize: 2));
        var firstPageAgain = await QueueAddressStore.GetAddresses(new PagingInfo(page: 1, pageSize: 2));
        var secondPage = await QueueAddressStore.GetAddresses(new PagingInfo(page: 2, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPage.Results, Has.Count.EqualTo(2), "two addresses on the first page");
            Assert.That(secondPage.Results, Has.Count.EqualTo(1), "and the third on the second, so the bodies differ");
        }

        return new(firstPage.QueryStats.Version, firstPageAgain.QueryStats.Version, secondPage.QueryStats.Version);
    }

    async Task<Queried> GroupErrorsTwoPages()
    {
        var group = NewGroup(ExceptionClassifier);

        await Insert(InGroup(group, Oldest), InGroup(group, Middle), InGroup(group, Newest));

        var firstPage = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo(page: 1, pageSize: 2));
        var firstPageAgain = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo(page: 1, pageSize: 2));
        var secondPage = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo(page: 2, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPage.Results, Has.Count.EqualTo(2), "two failures on the first page");
            Assert.That(secondPage.Results, Has.Count.EqualTo(1), "and the third on the second, so the bodies differ");
        }

        return new(firstPage.QueryStats.Version, firstPageAgain.QueryStats.Version, secondPage.QueryStats.Version);
    }

    async Task<Queried> GroupErrorCountTwoStatuses()
    {
        var group = NewGroup(ExceptionClassifier);
        var toArchive = InGroup(group, Middle);

        await Insert(InGroup(group, Oldest), toArchive, InGroup(group, Newest));
        await Archive(toArchive);

        var unresolved = await GroupsStore.GetGroupErrorsCount(group.Id, "unresolved", null);
        var unresolvedAgain = await GroupsStore.GetGroupErrorsCount(group.Id, "unresolved", null);
        var archived = await GroupsStore.GetGroupErrorsCount(group.Id, "archived", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unresolved.TotalCount, Is.EqualTo(2), "two of the three are still unresolved");
            Assert.That(archived.TotalCount, Is.EqualTo(1), "and one is archived, so the two responses carry different counts");
        }

        return new(unresolved.Version, unresolvedAgain.Version, archived.Version);
    }

    async Task<Queried> ArchivedGroupsTwoClassifiers()
    {
        var byException = NewGroup(ExceptionClassifier);
        var byMessageType = NewGroup(MessageTypeClassifier);

        // One failure filed under both classifiers, so each classifier has exactly one group to
        // report and the two groups differ in their id and their type.
        var failure = new IngestedFailure
        {
            Groups = [byException, byMessageType],
            AttemptedAt = Middle,
            TimeOfFailure = Middle,
            TimeSent = Middle.AddMinutes(-1)
        };

        await Insert(failure);
        await Archive(failure);

        var exceptionType = await GroupsStore.GetArchivedGroupsByClassifier(ExceptionClassifier);
        var exceptionTypeAgain = await GroupsStore.GetArchivedGroupsByClassifier(ExceptionClassifier);
        var messageType = await GroupsStore.GetArchivedGroupsByClassifier(MessageTypeClassifier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exceptionType.Results, Has.Count.EqualTo(1), "one archived group by exception type");
            Assert.That(messageType.Results, Has.Count.EqualTo(1), "one archived group by message type");
            Assert.That(messageType.Results[0].Id, Is.Not.EqualTo(exceptionType.Results[0].Id), "and they are not the same group");
        }

        return new(exceptionType.QueryStats.Version, exceptionTypeAgain.QueryStats.Version, messageType.QueryStats.Version);
    }

    async Task<Queried> CustomChecksTwoEmptyStatuses()
    {
        var failing = await CustomChecks.GetStats(new PagingInfo(), "fail");
        var failingAgain = await CustomChecks.GetStats(new PagingInfo(), "fail");
        var passing = await CustomChecks.GetStats(new PagingInfo(), "pass");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failing.Results, Is.Empty, "no failing checks");
            Assert.That(passing.Results, Is.Empty, "and no passing ones, so neither has rows to be named by");
        }

        return new(failing.QueryStats.Version, failingAgain.QueryStats.Version, passing.QueryStats.Version);
    }

    async Task<Queried> CustomChecksTwoEmptyPages()
    {
        await ReportCheck("Disk space");

        var third = await CustomChecks.GetStats(new PagingInfo(page: 3, pageSize: 1));
        var thirdAgain = await CustomChecks.GetStats(new PagingInfo(page: 3, pageSize: 1));
        var fourth = await CustomChecks.GetStats(new PagingInfo(page: 4, pageSize: 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(third.Results, Is.Empty, "page three is past the only check");
            Assert.That(fourth.Results, Is.Empty, "and so is page four");
        }

        return new(third.QueryStats.Version, thirdAgain.QueryStats.Version, fourth.QueryStats.Version);
    }

    async Task<Queried> QueueAddressesTwoEmptyPages()
    {
        await Ingest(Failure("Shipping@machine1"));
        await CompleteDatabaseOperation();

        var third = await QueueAddressStore.GetAddresses(new PagingInfo(page: 3, pageSize: 1));
        var thirdAgain = await QueueAddressStore.GetAddresses(new PagingInfo(page: 3, pageSize: 1));
        var fourth = await QueueAddressStore.GetAddresses(new PagingInfo(page: 4, pageSize: 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(third.Results, Is.Empty, "page three is past the only address");
            Assert.That(fourth.Results, Is.Empty, "and so is page four");
        }

        return new(third.QueryStats.Version, thirdAgain.QueryStats.Version, fourth.QueryStats.Version);
    }

    async Task<Queried> GroupErrorsTwoEmptyPages()
    {
        var group = NewGroup(ExceptionClassifier);

        await Insert(InGroup(group, Oldest));

        var third = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo(page: 3, pageSize: 1));
        var thirdAgain = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo(page: 3, pageSize: 1));
        var fourth = await GroupsStore.GetGroupErrors(group.Id, "unresolved", null, new SortInfo(), new PagingInfo(page: 4, pageSize: 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(third.Results, Is.Empty, "page three is past the only failure");
            Assert.That(fourth.Results, Is.Empty, "and so is page four");
        }

        return new(third.QueryStats.Version, thirdAgain.QueryStats.Version, fourth.QueryStats.Version);
    }

    async Task<Queried> GroupErrorCountTwoEmptyStatuses()
    {
        var group = NewGroup(ExceptionClassifier);

        await Insert(InGroup(group, Oldest));

        var archived = await GroupsStore.GetGroupErrorsCount(group.Id, "archived", null);
        var archivedAgain = await GroupsStore.GetGroupErrorsCount(group.Id, "archived", null);
        var retryIssued = await GroupsStore.GetGroupErrorsCount(group.Id, "retryIssued", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(archived.TotalCount, Is.Zero, "nothing in the group is archived");
            Assert.That(retryIssued.TotalCount, Is.Zero, "and nothing has a retry issued, so both counts are the same number");
        }

        return new(archived.Version, archivedAgain.Version, retryIssued.Version);
    }

    async Task<Queried> ArchivedGroupsTwoEmptyClassifiers()
    {
        // One unarchived failure, so the index is not empty, filed under neither classifier being asked for.
        await Insert(InGroup(NewGroup("Endpoint Address"), Middle));

        var byException = await GroupsStore.GetArchivedGroupsByClassifier(ExceptionClassifier);
        var byExceptionAgain = await GroupsStore.GetArchivedGroupsByClassifier(ExceptionClassifier);
        var byMessageType = await GroupsStore.GetArchivedGroupsByClassifier(MessageTypeClassifier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byException.Results, Is.Empty, "nothing archived under exception type");
            Assert.That(byMessageType.Results, Is.Empty, "nor under message type, so neither has rows to be named by");
        }

        return new(byException.QueryStats.Version, byExceptionAgain.QueryStats.Version, byMessageType.QueryStats.Version);
    }

    async Task<Queried> MessagesViewTwoEmptyPages()
    {
        await Ingest(new IngestedFailure());
        await CompleteDatabaseOperation();

        var third = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 3, pageSize: 1), new SortInfo(), includeSystemMessages: true);
        var thirdAgain = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 3, pageSize: 1), new SortInfo(), includeSystemMessages: true);
        var fourth = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 4, pageSize: 1), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(third.Results, Is.Empty, "page three is past the only message");
            Assert.That(fourth.Results, Is.Empty, "and so is page four");
        }

        return new(third.QueryStats.Version, thirdAgain.QueryStats.Version, fourth.QueryStats.Version);
    }

    async Task<Queried> ErrorsTwoEmptyStatuses()
    {
        // One unresolved failure, so the index is not empty, and two filters that select none of it.
        // The rows are what usually tell one query from another, and neither of these has any.
        await Ingest(new IngestedFailure());
        await CompleteDatabaseOperation();

        var archived = await FailedMessageQueryStore.GetFailedMessages("archived", null, null, new PagingInfo(), new SortInfo());
        var archivedAgain = await FailedMessageQueryStore.GetFailedMessages("archived", null, null, new PagingInfo(), new SortInfo());
        var retryIssued = await FailedMessageQueryStore.GetFailedMessages("retryIssued", null, null, new PagingInfo(), new SortInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(archived.Results, Is.Empty, "nothing is archived");
            Assert.That(retryIssued.Results, Is.Empty, "and nothing has a retry issued, so neither has rows to be named by");
        }

        return new(archived.QueryStats.Version, archivedAgain.QueryStats.Version, retryIssued.QueryStats.Version);
    }

    async Task<Queried> MessagesViewTwoPages()
    {
        await Ingest(new IngestedFailure(), new IngestedFailure(), new IngestedFailure());
        await CompleteDatabaseOperation();

        var firstPage = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 1, pageSize: 2), new SortInfo(), includeSystemMessages: true);
        var firstPageAgain = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 1, pageSize: 2), new SortInfo(), includeSystemMessages: true);
        var secondPage = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 2, pageSize: 2), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPage.Results, Has.Count.EqualTo(2), "two messages on the first page");
            Assert.That(secondPage.Results, Has.Count.EqualTo(1), "and the third on the second, so the bodies differ");
        }

        return new(firstPage.QueryStats.Version, firstPageAgain.QueryStats.Version, secondPage.QueryStats.Version);
    }

    async Task ReportCheck(string customCheckId, bool hasFailed = false)
    {
        await CustomChecks.UpdateCustomCheckStatus(new CustomCheckDetail
        {
            Category = "test-category",
            CustomCheckId = customCheckId,
            HasFailed = hasFailed,
            FailureReason = hasFailed ? "Testing" : null,
            ReportedAt = ReportedAt,
            OriginatingEndpoint = new EndpointDetails
            {
                Host = "localhost",
                HostId = Guid.Parse("55D0800D-CC90-47C3-83EB-DDE292140C28"),
                Name = "test-host"
            }
        });

        await CompleteDatabaseOperation();
    }

    static IngestedFailure Failure(string failingEndpointAddress) =>
        new() { FailingEndpointAddress = failingEndpointAddress };

    static FailedMessage.FailureGroup NewGroup(string classifier) =>
        new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = classifier };

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

    internal sealed record Scenario(string Name, string Because, Func<PagedVersionConformanceTests, Task<Queried>> Run)
    {
        public override string ToString() => Name;
    }

    internal sealed record Queried(DataVersion First, DataVersion FirstAgain, DataVersion Second);
}
