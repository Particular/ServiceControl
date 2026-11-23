namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Recoverability;

[TestFixture]
[NonParallelizable]
class RetryOperationsMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task Copies_one_document_into_both_target_tables()
    {
        var history = new RetryHistory();
        history.HistoricOperations.Add(new HistoricRetryOperation
        {
            RequestId = "group-1",
            RetryType = RetryType.ByQueueAddress,
            StartTime = CompletedAt.AddMinutes(-10),
            CompletionTime = CompletedAt.AddMinutes(-9),
            Originator = "Sales.Orders",
            Failed = true,
            NumberOfMessagesProcessed = 5
        });
        history.HistoricOperations.Add(HistoricOperation("group-3", CompletedAt.AddMinutes(-20)));
        history.UnacknowledgedOperations.Add(new UnacknowledgedRetryOperation
        {
            RequestId = "group-2",
            RetryType = RetryType.FailureGroup,
            StartTime = CompletedAt.AddMinutes(-5),
            CompletionTime = CompletedAt.AddMinutes(-4),
            Last = DateTime.MaxValue,
            Originator = "Shipping",
            Classifier = "Shipping-Failures",
            Failed = true,
            NumberOfMessagesProcessed = 2
        });

        await SeedSourceRetryHistory(history);

        var result = await CopyCategory("RetryOperations");

        Assert.That(result.Copied, Is.EqualTo(3));
        Assert.That(await Target.Count(MigrationCategoryRegistry.Find(MigrationCategoryIds.RetryOperations)), Is.EqualTo(3), "the category counts both tables, and a count of either table alone would report 2 or 1");

        var stored = (await RetryHistoryStore.GetRetryHistory()).Results;
        Assert.That(stored.HistoricOperations.Select(Columns), Is.EqualTo(history.HistoricOperations.Select(Columns)), "every column of the historic operation arrives as the source holds it");
        Assert.That(stored.UnacknowledgedOperations.Select(Columns), Is.EqualTo(history.UnacknowledgedOperations.Select(Columns)), "every column of the pending operation arrives as the source holds it, even a Last of DateTime.MaxValue, which InMemoryRetry records when it has no last time");
    }

    [Test]
    public async Task Duplicate_pending_operations_with_one_key_copy_once_and_count_the_rest_as_already_present()
    {
        var history = new RetryHistory();
        history.UnacknowledgedOperations.Add(PendingOperation("group-2", CompletedAt, originator: "First in the document"));
        history.UnacknowledgedOperations.Add(PendingOperation("group-2", CompletedAt.AddMinutes(1), originator: "Second in the document"));

        await SeedSourceRetryHistory(history);

        var result = await CopyCategory("RetryOperations");

        Assert.That(result.Copied, Is.EqualTo(1));
        Assert.That(result.AlreadyPresent, Is.EqualTo(1), "the second lands on the key the first fills, so it loses no row the target lacks and is neither a skip nor a primary-key violation");
        Assert.That(result.Skipped, Is.Zero);

        var stored = (await RetryHistoryStore.GetRetryHistory()).Results;
        Assert.That(stored.UnacknowledgedOperations.Single().Originator, Is.EqualTo("Second in the document"), "RavenDB appends a later retry of the same group, and the SQL store keeps the latest one, so the copy keeps the last in document order");
    }

    [Test]
    public async Task Copies_more_operations_than_one_statement_could_carry()
    {
        var history = new RetryHistory();

        foreach (var index in Enumerable.Range(0, OperationsPerList))
        {
            history.HistoricOperations.Add(HistoricOperation($"historic-{index}", CompletedAt.AddMinutes(index)));
            history.UnacknowledgedOperations.Add(PendingOperation($"pending-{index}", CompletedAt));
        }

        await SeedSourceRetryHistory(history);

        var result = await CopyCategory("RetryOperations");

        Assert.That(result.Copied, Is.EqualTo(2 * OperationsPerList));

        var stored = (await RetryHistoryStore.GetRetryHistory()).Results;
        Assert.That(stored.HistoricOperations.Select(operation => operation.RequestId), Is.EquivalentTo(history.HistoricOperations.Select(operation => operation.RequestId)));
        Assert.That(stored.UnacknowledgedOperations.Select(operation => operation.RequestId), Is.EquivalentTo(history.UnacknowledgedOperations.Select(operation => operation.RequestId)));
    }

    // More rows than SQL Server's 2,000 parameters could carry at even one column each.
    const int OperationsPerList = 2_100;

    // Whole seconds, because PostgreSQL stores microseconds and would not give back a time with more precision.
    static readonly DateTime CompletedAt = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    static (string, RetryType, DateTime, DateTime, string, bool, int) Columns(HistoricRetryOperation operation) =>
        (operation.RequestId, operation.RetryType, operation.StartTime, operation.CompletionTime, operation.Originator, operation.Failed, operation.NumberOfMessagesProcessed);

    static (string, RetryType, DateTime, DateTime, DateTime, string, string, bool, int) Columns(UnacknowledgedRetryOperation operation) =>
        (operation.RequestId, operation.RetryType, operation.StartTime, operation.CompletionTime, operation.Last, operation.Originator, operation.Classifier, operation.Failed, operation.NumberOfMessagesProcessed);

    internal static HistoricRetryOperation HistoricOperation(string requestId, DateTime completedAt) => new()
    {
        RequestId = requestId,
        RetryType = RetryType.FailureGroup,
        StartTime = completedAt.AddMinutes(-1),
        CompletionTime = completedAt,
        Originator = "Sales.Orders",
        NumberOfMessagesProcessed = 1
    };

    internal static UnacknowledgedRetryOperation PendingOperation(string requestId, DateTime completedAt, string originator = "Shipping") => new()
    {
        RequestId = requestId,
        RetryType = RetryType.FailureGroup,
        StartTime = completedAt.AddMinutes(-1),
        CompletionTime = completedAt,
        Last = completedAt,
        Originator = originator,
        Classifier = "Shipping-Failures",
        NumberOfMessagesProcessed = 1
    };
}
