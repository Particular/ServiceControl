namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class RetryHistoryVersionTests : PersistenceTestBase
{
    const int DefaultDepth = 10;

    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Version_changes_when_an_operation_is_acknowledged()
    {
        await RecordCompleted("group-1");
        await CompleteDatabaseOperation();

        var before = await RetryHistoryStore.GetRetryHistory();

        var acknowledged = await RetryHistoryStore.AcknowledgeRetryGroup("group-1");
        await CompleteDatabaseOperation();

        var after = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(acknowledged, Is.True, "the premise: there was something to acknowledge");
            Assert.That(before.Results.UnacknowledgedOperations, Has.Count.EqualTo(1));
            Assert.That(after.Results.UnacknowledgedOperations, Is.Empty, "the body changed");
            Assert.That(after.Results.HistoricOperations, Has.Count.EqualTo(1), "and the historic half did not");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too, or a revalidating client keeps an operation it has dismissed");
        }
    }

    [Test]
    public async Task Version_changes_when_an_operation_completes()
    {
        await RecordCompleted("group-1");
        await CompleteDatabaseOperation();

        var before = await RetryHistoryStore.GetRetryHistory();

        await RecordCompleted("group-2", completionTime: Noon.AddHours(1));
        await CompleteDatabaseOperation();

        var after = await RetryHistoryStore.GetRetryHistory();

        VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version,
            "another operation completed, so a revalidating client must not keep the old history");
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        await RecordCompleted("group-1");
        await CompleteDatabaseOperation();

        var first = await RetryHistoryStore.GetRetryHistory();
        var second = await RetryHistoryStore.GetRetryHistory();

        VersionAssert.Matches(first.QueryStats.Version, second.QueryStats.Version,
            "nothing changed, so the validator has to stay put or conditional GET never pays off");
    }

    [Test]
    public async Task An_empty_history_still_reports_a_version()
    {
        var result = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.HistoricOperations, Is.Empty);
            Assert.That(result.QueryStats.Version.HasValue, Is.True,
                "an empty history is a representation like any other and has to be cacheable");
        }
    }

    Task RecordCompleted(string requestId, DateTime? completionTime = null)
    {
        var completed = completionTime ?? Noon;

        return RetryHistoryStore.RecordRetryOperationCompleted(requestId, RetryType.FailureGroup,
            completed.AddMinutes(-5), completed, "OrderPlaced failures", "Exception Type and Stack Trace",
            messageFailed: false, numberOfMessagesProcessed: 1, completed.AddMinutes(-1), DefaultDepth);
    }
}
