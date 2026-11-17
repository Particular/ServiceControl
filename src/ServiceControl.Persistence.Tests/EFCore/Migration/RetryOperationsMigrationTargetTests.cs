namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Recoverability;

class RetryOperationsMigrationTargetTests : PersistenceTestBase
{
    readonly TargetWarnings targetLog = new();

    public RetryOperationsMigrationTargetTests() => RegisterServices = services => services.AddSingleton<ILoggerProvider>(targetLog);

    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory RetryOperationsCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.RetryOperations)!;

    static readonly DateTime CompletedAt = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task A_retry_operation_with_a_value_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var fourHundredCodePoints = char.ConvertFromUtf32(0x1F600) + new string('r', 399);
        var countsCodePoints = dialect.TextLength(fourHundredCodePoints) < fourHundredCodePoints.Length;
        var batch = BatchOf(
            ("historic/1", new RetryOperationRow(Historic(new string('r', 400)), null)),
            ("historic/2", new RetryOperationRow(Historic(new string('r', 401)), null)),
            ("historic/3", new RetryOperationRow(Historic(fourHundredCodePoints), null)),
            ("pending/1", new RetryOperationRow(null, Pending(new string('p', 401), "Shipping-Failures"))),
            ("pending/2", new RetryOperationRow(null, Pending("group-2", new string('c', 451)))),
            ("pending/3", new RetryOperationRow(null, Pending(new string('p', 400), new string('c', 450)))));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new RetryOperationsWriter(dialect), batch);
        var result = await Target.Write(RetryOperationsCategory, batch, CheckpointAfter("pending/3"));
        var stored = (await RetryHistoryStore.GetRetryHistory()).Results!;
        var historicRequestId = PreparedSkips.TableAndColumnOf<HistoricRetryOperationEntity>(ServiceProvider, nameof(HistoricRetryOperationEntity.RequestId));
        string[] expectedDetails =
        [
            $"{historicRequestId} takes at most 400 characters and the value holds 401, on document 'historic/2'",
            .. countsCodePoints ? Array.Empty<string>() : new[] { $"{historicRequestId} takes at most 400 characters and the value holds 401, on document 'historic/3'" },
            $"{PreparedSkips.TableAndColumnOf<UnacknowledgedRetryOperationEntity>(ServiceProvider, nameof(UnacknowledgedRetryOperationEntity.RequestId))} takes at most 400 characters and the value holds 401, on document 'pending/1'",
            $"{PreparedSkips.TableAndColumnOf<UnacknowledgedRetryOperationEntity>(ServiceProvider, nameof(UnacknowledgedRetryOperationEntity.Classifier))} takes at most 450 characters and the value holds 451, on document 'pending/2'"
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fourHundredCodePoints, Has.Length.EqualTo(401), "the emoji request id is one UTF-16 unit over the column, and 400 code points");
            Assert.That(result.Copied, Is.EqualTo(countsCodePoints ? 3 : 2), "a value exactly as long as its column fits, in either table");
            Assert.That(result.SkippedIds, Is.EqualTo(countsCodePoints ? new[] { "historic/2", "pending/1", "pending/2" } : new[] { "historic/2", "historic/3", "pending/1", "pending/2" }),
                "400 code points fit a column that counts code points, and a skip there would lose a retry operation the database could hold");
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = countsCodePoints ? 3 : 4 }), "an over-long value would fail the whole insert and halt the category");
            Assert.That(stored.HistoricOperations.Select(operation => operation.RequestId), Is.EquivalentTo(countsCodePoints ? new[] { new string('r', 400), fourHundredCodePoints } : new[] { new string('r', 400) }));
            Assert.That(stored.UnacknowledgedOperations.Select(operation => (operation.RequestId, operation.Classifier)), Is.EqualTo(new[] { (new string('p', 400), new string('c', 450)) }));
            Assert.That(details, Is.EqualTo(expectedDetails));
        }
    }

    [Test]
    public async Task A_retry_operation_holding_a_character_the_database_refuses_is_skipped_as_malformed_value_where_the_database_refuses_it()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var refused = dialect.RejectedCharacterIn("\0") is not null;
        var historic = Historic("group-1");
        historic.Originator = "Sales\0Orders";
        var pending = Pending("group-2", "Shipping-Failures");
        pending.Originator = "Ship\0ping";
        var batch = BatchOf(
            ("historic/1", new RetryOperationRow(historic, null)),
            ("historic/2", new RetryOperationRow(Historic("group-3"), null)),
            ("pending/1", new RetryOperationRow(null, pending)),
            ("pending/2", new RetryOperationRow(null, Pending("group-4", "Shipping-Failures"))));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new RetryOperationsWriter(dialect), batch);
        var result = await Target.Write(RetryOperationsCategory, batch, CheckpointAfter("pending/2"));
        var stored = (await RetryHistoryStore.GetRetryHistory()).Results!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SkippedIds, Is.EqualTo(refused ? new[] { "historic/1", "pending/1" } : Array.Empty<string>()), "only a database that refuses the character fails the statement over it");
            Assert.That(stored.HistoricOperations.Select(operation => operation.RequestId), Is.EquivalentTo(refused ? new[] { "group-3" } : new[] { "group-1", "group-3" }));
            Assert.That(stored.UnacknowledgedOperations.Select(operation => operation.RequestId), Is.EquivalentTo(refused ? new[] { "group-4" } : new[] { "group-2", "group-4" }));

            if (refused)
            {
                Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.MalformedValue] = 2 }));
                Assert.That(details, Is.EqualTo(new[]
                {
                    $"{PreparedSkips.TableAndColumnOf<HistoricRetryOperationEntity>(ServiceProvider, nameof(HistoricRetryOperationEntity.Originator))} cannot hold the character U+0000, which the value contains, on document 'historic/1'",
                    $"{PreparedSkips.TableAndColumnOf<UnacknowledgedRetryOperationEntity>(ServiceProvider, nameof(UnacknowledgedRetryOperationEntity.Originator))} cannot hold the character U+0000, which the value contains, on document 'pending/1'"
                }));
            }
        }
    }

    [Test]
    public async Task A_retry_operation_with_no_request_id_is_skipped_naming_the_table_and_the_rest_copy()
    {
        var batch = BatchOf(
            ("historic/1", new RetryOperationRow(Historic(null!), null)),
            ("historic/2", new RetryOperationRow(Historic("group-1"), null)),
            ("pending/1", new RetryOperationRow(null, Pending(null!, "Shipping-Failures"))),
            ("pending/2", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures"))));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new RetryOperationsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(RetryOperationsCategory, batch, CheckpointAfter("pending/2"));
        var stored = (await RetryHistoryStore.GetRetryHistory()).Results!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(details, Is.EqualTo(new[]
            {
                "HistoricRetryOperations.RequestId is NOT NULL and the document has no value for it",
                "UnacknowledgedRetryOperations.RequestId is NOT NULL and the document has no value for it"
            }), "the target logs this line, so it has to name the table the operation was bound for");
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "historic/1", "pending/1" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2 }));
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(stored.HistoricOperations.Select(operation => operation.RequestId), Is.EqualTo(new[] { "group-1" }));
            Assert.That(stored.UnacknowledgedOperations.Select(operation => operation.RequestId), Is.EqualTo(new[] { "group-2" }));
        }
    }

    [Test]
    public async Task A_second_pending_operation_with_the_same_key_is_reported_as_a_merge()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("historic/1", new RetryOperationRow(Historic("group-2"), null)),
            ("pending/skipped", new RetryOperationRow(null, Pending(null!, "Shipping-Failures"))),
            ("pending/1", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures"))),
            ("pending/2", new RetryOperationRow(null, Pending("group-2", "Billing-Failures"))),
            ("pending/3", new RetryOperationRow(null, Pending("GROUP-2", "Shipping-Failures"))),
            ("pending/4", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures", RetryType.ByQueueAddress))));
        var requestIdIgnoresCase = dialect.KeyComparer(typeof(UnacknowledgedRetryOperationEntity), nameof(UnacknowledgedRetryOperationEntity.RequestId)).Equals("group-2", "GROUP-2");

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new RetryOperationsWriter(dialect), batch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(requestIdIgnoresCase ? new[] { "pending/2", "pending/1" } : new[] { "pending/1" }),
                "the last pending operation with one key is kept and the earlier ones are dropped on every database, a case-only request id counts as the same key only where the column ignores case, one request retried as another type never merges, and a historic or skipped row never merges");
            Assert.That(merges[0].Detail, Does.Contain(requestIdIgnoresCase ? "kept 'GROUP-2', 'FailureGroup' and dropped the row for 'group-2', 'FailureGroup'" : "kept 'group-2', 'FailureGroup' and dropped the row for 'group-2', 'FailureGroup'"),
                "the target logs this line, so it has to name the key it kept and the key it dropped without swapping them");
        }
    }

    [Test]
    public async Task A_merged_pending_operation_names_the_source_row_kept_and_the_row_dropped()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("unacknowledged|group-2|FailureGroup|older", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures"))),
            ("unacknowledged|group-2|FailureGroup|newer", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures"))));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new RetryOperationsWriter(dialect), batch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(new[] { "unacknowledged|group-2|FailureGroup|older" }), "the latest retry wins");
            Assert.That(merges.Single().Detail, Does.EndWith("It kept the row from 'unacknowledged|group-2|FailureGroup|newer' and dropped the row from 'unacknowledged|group-2|FailureGroup|older'."),
                "both keys print the same, so only the source rows, whose ids carry the completion time, tell the operator which run survived");
        }
    }

    [Test]
    public async Task A_pending_key_the_target_already_holds_merges_only_a_variant_the_column_treats_as_the_same()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var requestIdIgnoresCase = dialect.KeyComparer(typeof(UnacknowledgedRetryOperationEntity), nameof(UnacknowledgedRetryOperationEntity.RequestId)).Equals("group-2", "GROUP-2");
        var first = await Target.Write(RetryOperationsCategory, BatchOf(("pending/1", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures")))), CheckpointAfter("pending/1"));

        // The case variant comes first, so on a case-insensitive column only the stored row can be what it merges with.
        var second = BatchOf(
            ("pending/2", new RetryOperationRow(null, Pending("GROUP-2", "Shipping-Failures"))),
            ("pending/3", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures"))),
            ("pending/4", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures", RetryType.ByQueueAddress))),
            ("historic/1", new RetryOperationRow(Historic("group-2"), null)));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new RetryOperationsWriter(dialect), second);
        var result = await Target.Write(RetryOperationsCategory, second, first.Saved with { Cursor = second.Cursor });
        var stored = (await RetryHistoryStore.GetRetryHistory()).Results!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(requestIdIgnoresCase ? new[] { "pending/2" } : Array.Empty<string>()),
                "an exact repeat of a stored key is an earlier pass's row, a case variant merges only where the column ignores case, and another retry type never");

            if (requestIdIgnoresCase)
            {
                Assert.That(merges[0].Detail, Does.Contain("kept 'group-2', 'FailureGroup' and dropped the row for 'GROUP-2', 'FailureGroup'"));
            }

            Assert.That(result.Copied, Is.EqualTo(requestIdIgnoresCase ? 2 : 3), "the historic row and the other retry type are copied, and so is the case variant where the column tells it apart");
            Assert.That(result.AlreadyPresent, Is.EqualTo(requestIdIgnoresCase ? 2 : 1));
            Assert.That(stored.UnacknowledgedOperations.Select(operation => (operation.RequestId, operation.RetryType)), Is.EquivalentTo(requestIdIgnoresCase
                ? new[] { ("group-2", RetryType.FailureGroup), ("group-2", RetryType.ByQueueAddress) }
                : new[] { ("group-2", RetryType.FailureGroup), ("GROUP-2", RetryType.FailureGroup), ("group-2", RetryType.ByQueueAddress) }));
            Assert.That(stored.HistoricOperations.Select(operation => operation.RequestId), Is.EqualTo(new[] { "group-2" }));
        }
    }

    [Test]
    public async Task A_retry_operation_time_of_local_or_unspecified_kind_is_copied_as_the_same_instant()
    {
        var historic = Historic("group-1");
        historic.StartTime = CompletedAt.AddMinutes(-1).ToLocalTime();
        historic.CompletionTime = DateTime.SpecifyKind(CompletedAt, DateTimeKind.Unspecified);
        var pending = Pending("group-2", "Shipping-Failures");
        pending.StartTime = DateTime.SpecifyKind(CompletedAt.AddMinutes(-1), DateTimeKind.Unspecified);
        pending.CompletionTime = CompletedAt.ToLocalTime();
        pending.Last = CompletedAt.AddMinutes(1).ToLocalTime();
        var batch = BatchOf(
            ("historic/1", new RetryOperationRow(historic, null)),
            ("pending/1", new RetryOperationRow(null, pending)));

        var result = await Target.Write(RetryOperationsCategory, batch, CheckpointAfter("pending/1"));
        var stored = (await RetryHistoryStore.GetRetryHistory()).Results!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2), "Npgsql refuses a time that is not UTC for a timestamptz column, and that would fail the whole retry history");
            Assert.That(stored.HistoricOperations.Select(operation => (operation.StartTime, operation.CompletionTime)), Is.EqualTo(new[] { (CompletedAt.AddMinutes(-1), CompletedAt) }),
                "an Unspecified time is already UTC and a Local time is converted, so both keep their instant");
            Assert.That(stored.UnacknowledgedOperations.Select(operation => (operation.StartTime, operation.CompletionTime, operation.Last)), Is.EqualTo(new[] { (CompletedAt.AddMinutes(-1), CompletedAt, CompletedAt.AddMinutes(1)) }));
        }
    }

    [Test]
    public async Task Copied_historic_operations_get_one_line_saying_the_database_gives_them_new_ids()
    {
        var first = await Target.Write(RetryOperationsCategory, BatchOf(("historic/1", new RetryOperationRow(Historic("group-1"), null)), ("pending/1", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures")))), CheckpointAfter("pending/1"));
        await Target.Write(RetryOperationsCategory, BatchOf(("historic/2", new RetryOperationRow(Historic("group-3"), null))), first.Saved with { Cursor = "historic/2" });

        Assert.That(targetLog.Information, Is.EqualTo(new[] { $"Category {MigrationCategoryIds.RetryOperations}: the database gives the copied historic retry operations new ids. Nothing in SQL refers to the ids they had in the source." }),
            "one line per category says the ids changed, rather than one per row or per batch");
    }

    [Test]
    public async Task Copied_pending_operations_alone_get_no_line_about_new_ids()
    {
        await Target.Write(RetryOperationsCategory, BatchOf(("pending/1", new RetryOperationRow(null, Pending("group-2", "Shipping-Failures")))), CheckpointAfter("pending/1"));

        Assert.That(targetLog.Information, Has.None.Contains("new ids"), "a pending operation keeps its key, so nothing about it is renumbered");
    }

    static HistoricRetryOperation Historic(string requestId) => new()
    {
        RequestId = requestId,
        RetryType = RetryType.ByQueueAddress,
        StartTime = CompletedAt.AddMinutes(-1),
        CompletionTime = CompletedAt,
        Originator = "Sales.Orders",
        NumberOfMessagesProcessed = 1
    };

    static UnacknowledgedRetryOperation Pending(string requestId, string classifier, RetryType retryType = RetryType.FailureGroup) => new()
    {
        RequestId = requestId,
        RetryType = retryType,
        StartTime = CompletedAt.AddMinutes(-1),
        CompletionTime = CompletedAt,
        Last = CompletedAt,
        Originator = "Shipping",
        Classifier = classifier,
        NumberOfMessagesProcessed = 1
    };

    static MigrationBatch BatchOf(params (string Id, RetryOperationRow Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.RetryOperations, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
