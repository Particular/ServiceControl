namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Recoverability;

class RetryHistoryDataStoreTests : ErrorIngestionTestBase
{
    const int DefaultDepth = 10;

    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Returns_an_empty_history_when_nothing_has_completed()
    {
        var history = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(history.HistoricOperations, Is.Empty);
            Assert.That(history.UnacknowledgedOperations, Is.Empty);
        }
    }

    [Test]
    public async Task Records_a_completed_operation()
    {
        await RecordCompleted("group-1", originator: "OrderPlaced failures", classifier: "Exception Type and Stack Trace",
            failed: true, numberOfMessagesProcessed: 3);

        var history = await RetryHistoryStore.GetRetryHistory();

        var historic = history.HistoricOperations.Single();
        var unacknowledged = history.UnacknowledgedOperations.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(historic.RequestId, Is.EqualTo("group-1"));
            Assert.That(historic.RetryType, Is.EqualTo(RetryType.FailureGroup));
            Assert.That(historic.StartTime, Is.EqualTo(Noon.AddMinutes(-5)));
            Assert.That(historic.CompletionTime, Is.EqualTo(Noon));
            Assert.That(historic.Originator, Is.EqualTo("OrderPlaced failures"));
            Assert.That(historic.Failed, Is.True);
            Assert.That(historic.NumberOfMessagesProcessed, Is.EqualTo(3));

            Assert.That(unacknowledged.RequestId, Is.EqualTo("group-1"));
            Assert.That(unacknowledged.RetryType, Is.EqualTo(RetryType.FailureGroup));
            Assert.That(unacknowledged.StartTime, Is.EqualTo(Noon.AddMinutes(-5)));
            Assert.That(unacknowledged.CompletionTime, Is.EqualTo(Noon));
            Assert.That(unacknowledged.Last, Is.EqualTo(Noon.AddMinutes(-1)));
            Assert.That(unacknowledged.Originator, Is.EqualTo("OrderPlaced failures"));
            Assert.That(unacknowledged.Classifier, Is.EqualTo("Exception Type and Stack Trace"));
            Assert.That(unacknowledged.Failed, Is.True);
            Assert.That(unacknowledged.NumberOfMessagesProcessed, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Returns_the_newest_operations_first()
    {
        await RecordCompleted("group-1", completionTime: Noon);
        await RecordCompleted("group-2", completionTime: Noon.AddHours(-1));
        await RecordCompleted("group-3", completionTime: Noon.AddHours(1));

        var history = await RetryHistoryStore.GetRetryHistory();

        Assert.That(history.HistoricOperations.Select(operation => operation.RequestId),
            Is.EqualTo(new[] { "group-3", "group-1", "group-2" }));
    }

    [Test]
    public async Task Keeps_only_the_newest_operations_up_to_the_depth()
    {
        for (var minute = 0; minute < 5; minute++)
        {
            await RecordCompleted($"group-{minute}", completionTime: Noon.AddMinutes(minute), depth: 3);
        }

        var history = await RetryHistoryStore.GetRetryHistory();

        Assert.That(history.HistoricOperations.Select(operation => operation.RequestId),
            Is.EqualTo(new[] { "group-4", "group-3", "group-2" }));
    }

    [Test]
    public async Task Breaks_ties_on_completion_time_by_the_order_recorded()
    {
        await RecordCompleted("group-1", completionTime: Noon, depth: 2);
        await RecordCompleted("group-2", completionTime: Noon, depth: 2);
        await RecordCompleted("group-3", completionTime: Noon, depth: 2);

        var history = await RetryHistoryStore.GetRetryHistory();

        Assert.That(history.HistoricOperations.Select(operation => operation.RequestId),
            Is.EqualTo(new[] { "group-3", "group-2" }));
    }

    [Test]
    public async Task Applies_a_reduced_depth_to_operations_already_recorded()
    {
        for (var minute = 0; minute < 4; minute++)
        {
            await RecordCompleted($"group-{minute}", completionTime: Noon.AddMinutes(minute));
        }

        await RecordCompleted("group-4", completionTime: Noon.AddMinutes(4), depth: 2);

        var history = await RetryHistoryStore.GetRetryHistory();

        Assert.That(history.HistoricOperations.Select(operation => operation.RequestId),
            Is.EqualTo(new[] { "group-4", "group-3" }));
    }

    [Test]
    public async Task Keeps_no_history_when_the_depth_is_zero()
    {
        await RecordCompleted("group-1");
        await RecordCompleted("group-2", depth: 0);

        var history = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(history.HistoricOperations, Is.Empty);
            Assert.That(history.UnacknowledgedOperations, Has.Count.EqualTo(2), "acknowledgements are not subject to the history depth");
        }
    }

    [TestCase(RetryType.SingleMessage)]
    [TestCase(RetryType.MultipleMessages)]
    public async Task Does_not_wait_for_an_acknowledgement_of_message_retries(RetryType retryType)
    {
        await RecordCompleted("request-1", retryType);

        var history = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(history.HistoricOperations, Has.Count.EqualTo(1));
            Assert.That(history.UnacknowledgedOperations, Is.Empty);
        }
    }

    [Test]
    public async Task Replaces_the_pending_acknowledgement_when_a_group_is_retried_again()
    {
        await RecordCompleted("group-1", completionTime: Noon, numberOfMessagesProcessed: 3);
        await RecordCompleted("group-1", completionTime: Noon.AddHours(1), numberOfMessagesProcessed: 7);

        var history = await RetryHistoryStore.GetRetryHistory();

        var unacknowledged = history.UnacknowledgedOperations.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unacknowledged.CompletionTime, Is.EqualTo(Noon.AddHours(1)));
            Assert.That(unacknowledged.NumberOfMessagesProcessed, Is.EqualTo(7));
            Assert.That(history.HistoricOperations, Has.Count.EqualTo(2), "the history keeps both completions");
        }
    }

    [Test]
    public async Task Keeps_the_pending_acknowledgements_of_other_retry_types_apart()
    {
        await RecordCompleted("request-1", RetryType.FailureGroup);
        await RecordCompleted("request-1", RetryType.AllForEndpoint);

        var history = await RetryHistoryStore.GetRetryHistory();

        Assert.That(history.UnacknowledgedOperations.Select(operation => operation.RetryType),
            Is.EquivalentTo(new[] { RetryType.FailureGroup, RetryType.AllForEndpoint }));
    }

    [Test]
    public async Task Acknowledges_a_group_retry()
    {
        await RecordCompleted("group-1");

        var acknowledged = await RetryHistoryStore.AcknowledgeRetryGroup("group-1");

        var history = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(acknowledged, Is.True);
            Assert.That(history.UnacknowledgedOperations, Is.Empty);
            Assert.That(history.HistoricOperations, Has.Count.EqualTo(1), "acknowledging does not erase the history");
        }
    }

    [Test]
    public async Task Does_not_acknowledge_an_unknown_group() =>
        Assert.That(await RetryHistoryStore.AcknowledgeRetryGroup("group-1"), Is.False);

    [Test]
    public async Task Does_not_acknowledge_an_operation_of_another_retry_type()
    {
        await RecordCompleted("SomeEndpoint", RetryType.AllForEndpoint);

        var acknowledged = await RetryHistoryStore.AcknowledgeRetryGroup("SomeEndpoint");

        var history = await RetryHistoryStore.GetRetryHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(acknowledged, Is.False);
            Assert.That(history.UnacknowledgedOperations, Has.Count.EqualTo(1));
        }
    }

    Task RecordCompleted(string requestId, RetryType retryType = RetryType.FailureGroup, DateTime? completionTime = null,
        string originator = "OrderPlaced failures", string classifier = "Exception Type and Stack Trace",
        bool failed = false, int numberOfMessagesProcessed = 1, int depth = DefaultDepth)
    {
        var completed = completionTime ?? Noon;

        return RetryHistoryStore.RecordRetryOperationCompleted(requestId, retryType, completed.AddMinutes(-5), completed,
            originator, classifier, failed, numberOfMessagesProcessed, completed.AddMinutes(-1), depth);
    }
}
