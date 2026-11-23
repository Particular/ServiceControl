namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Infrastructure;

class FailedErrorImportsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.FailedErrorImports)!;

    // Not a whole microsecond, because PostgreSQL keeps only microseconds and a re-read must still match what it stored.
    static readonly DateTime FirstFailure = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc).AddTicks(1234567);

    [Test]
    public async Task A_second_import_of_the_same_message_in_a_batch_is_reported_as_a_merge_naming_the_document_kept()
    {
        var batch = BatchOf(Row("FailedErrorImports/a", FirstFailure), Row("FailedErrorImports/b", FirstFailure.AddMinutes(5)));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, Writer(), batch);
        var result = await Target.Write(Category, batch, CheckpointAfter("FailedErrorImports/b"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(new[] { "FailedErrorImports/b" }), "6.18 and earlier stored each failed import attempt under its own document, and both derive one key");
            Assert.That(merges.Single().Detail, Does.Contain("kept the row from 'FailedErrorImports/a'"), "the target logs this line, so it has to name the document it kept");
            Assert.That((result.Copied, result.AlreadyPresent), Is.EqualTo((1, 1)));
        }
    }

    [Test]
    public async Task An_import_of_a_message_an_earlier_batch_copied_from_another_document_is_reported_as_a_merge()
    {
        var first = await Target.Write(Category, BatchOf(Row("FailedErrorImports/a", FirstFailure)), CheckpointAfter("FailedErrorImports/a"));
        var later = BatchOf(Row("FailedErrorImports/b", FirstFailure.AddMinutes(5)));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, Writer(), later);
        var result = await Target.Write(Category, later, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(new[] { "FailedErrorImports/b" }), "random document ids spread the imports of one message over different batches");
            Assert.That(merges.Single().Detail, Does.Contain("already holds an import of the same message"));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task A_document_an_earlier_pass_copied_is_already_present_and_not_a_merge()
    {
        var batch = BatchOf(Row("FailedErrorImports/a", FirstFailure));
        var first = await Target.Write(Category, batch, CheckpointAfter("FailedErrorImports/a"));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, Writer(), batch);
        var again = await Target.Write(Category, batch, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges, Is.Empty, "a retry re-reads the same documents, and finding its own rows again is not a merge");
            Assert.That(again.AlreadyPresent, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task An_import_with_no_exception_or_no_body_is_reported_as_a_change_once()
    {
        var batch = BatchOf(
            ImportRow("FailedErrorImports/no-exception", "native-1", exceptionInfo: null, [1]),
            ImportRow("FailedErrorImports/no-body", "native-2", "Deserialization failed", body: null),
            ImportRow("FailedErrorImports/neither", "native-3", exceptionInfo: null, body: null),
            ImportRow("FailedErrorImports/complete", "native-4", "Deserialization failed", [1]),
            ImportRow("FailedErrorImports/no-exception-again", "native-1", exceptionInfo: null, [1]));

        var changes = await PreparedSkips.ChangesFrom(ServiceProvider, Writer(), batch);
        var first = await Target.Write(Category, batch, CheckpointAfter("FailedErrorImports/no-exception-again"));
        var again = await Target.Write(Category, batch, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new[]
            {
                ("FailedErrorImports/no-exception", "Stored an empty exception, because the document has none."),
                ("FailedErrorImports/no-body", "Stored an empty body, because the document's message has none."),
                ("FailedErrorImports/neither", "Stored an empty exception and an empty body, because the document has neither.")
            }), "a complete import changes nothing, and the second import of one message is a merge, not a change");
            Assert.That((first.Changed, again.Changed), Is.EqualTo((3, 0)), "a re-read finds the rows an earlier pass copied and changes nothing");
        }
    }

    static MigrationRow ImportRow(string documentId, string messageId, string exceptionInfo, byte[] body) => new(
        documentId,
        new FailedErrorImport
        {
            Id = documentId,
            Message = new FailedTransportMessage { Id = messageId, Headers = new Dictionary<string, string> { ["NServiceBus.MessageId"] = messageId }, Body = body },
            ExceptionInfo = exceptionInfo
        },
        new Dictionary<string, object> { [MigrationMetadataKeys.FailedAt] = FirstFailure },
        new MigrationBody(body ?? [], "application/octet-stream"));

    FailedErrorImportsWriter Writer() => new(
        ServiceProvider.GetRequiredService<IMigrationSqlDialect>(),
        ServiceProvider.GetRequiredService<IBodyStoragePersistence>(),
        ServiceProvider.GetRequiredService<EFPersisterSettings>());

    static MigrationRow Row(string documentId, DateTime failedAt) => new(
        documentId,
        new FailedErrorImport
        {
            Id = documentId,
            Message = new FailedTransportMessage { Id = "native-8", Headers = new Dictionary<string, string> { ["NServiceBus.MessageId"] = "native-8" }, Body = [1, 2, 3] },
            ExceptionInfo = "Deserialization failed"
        },
        new Dictionary<string, object> { [MigrationMetadataKeys.FailedAt] = failedAt },
        new MigrationBody(new byte[] { 1, 2, 3 }, "application/octet-stream"));

    static MigrationBatch BatchOf(params MigrationRow[] rows) => new([.. rows], rows[^1].SourceId);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.FailedErrorImports, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
