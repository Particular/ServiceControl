namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;

[TestFixture]
[NonParallelizable]
class FailedErrorImportMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task Copies_a_small_body_inline_with_the_time_RavenDB_stored_the_import_as_its_failure_time()
    {
        await SeedSourceFailedErrorImport("native-1", [1, 2, 3], "Deserialization failed");

        var result = await CopyCategory(MigrationCategoryIds.FailedErrorImports);
        var import = await GetImport("native-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(await Target.Count(Category), Is.EqualTo(1), "Count is what --migration-verify reads");
            Assert.That(import.Body, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(import.BodyStoredExternally, Is.False);
            Assert.That(import.ExceptionInfo, Is.EqualTo("Deserialization failed"));
            Assert.That(import.FailedAt, Is.EqualTo(await SourceLastModified("native-1")).Within(TimeSpan.FromMilliseconds(1)), "replay runs oldest first by this time, so the time of the copy would reorder a customer's stuck imports");
            Assert.That(RecordedBodies.Written, Is.Empty);
        }
    }

    [Test]
    public async Task Migrated_bodies_replay_through_the_products_own_import_path()
    {
        byte[] small = [1, 2, 3];
        var large = LargeBody();
        await SeedSourceFailedErrorImport("native-3", small, "Deserialization failed");
        await SeedSourceFailedErrorImport("native-4", large, "Deserialization failed");

        await CopyCategory(MigrationCategoryIds.FailedErrorImports);

        var external = await GetImport("native-4");
        Assert.That(external.BodyStoredExternally, Is.True, "otherwise this proves nothing about the external placement");
        Assert.That(external.Body, Is.Empty, "the product keeps no inline copy of a body it stores in body storage");

        var replayed = new Dictionary<string, FailedTransportMessage>();
        await FailedImportStore.ProcessFailedErrorImports((message, _) =>
        {
            replayed[message.Id] = message;
            return Task.CompletedTask;
        }, TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed.GetValueOrDefault("native-3")?.Body, Is.EqualTo(small), "an inline body is read from the row");
            Assert.That(replayed.GetValueOrDefault("native-4")?.Body, Is.EqualTo(large), "an external body is read from body storage under the body id the SQL import store uses for that key");
            Assert.That(replayed.GetValueOrDefault("native-3")?.Headers, Is.EqualTo(HeadersFor("native-3")), "the replay ingests the message under the headers the copy stored");
            Assert.That(replayed.GetValueOrDefault("native-4")?.Headers, Is.EqualTo(HeadersFor("native-4")));
        }
    }

    [Test]
    public async Task A_retry_finds_every_import_already_present_and_writes_no_body_a_second_time()
    {
        await SeedSourceFailedErrorImport("native-2", [9], "Deserialization failed");
        await SeedSourceFailedErrorImport("native-7", LargeBody(), "Deserialization failed");
        await CopyCategory(MigrationCategoryIds.FailedErrorImports);
        var bodiesWritten = RecordedBodies.Written.Count;

        await Target.ResetForRetry(RetryResetMigrationTests.ResetForRetry(await ReadCheckpoint(MigrationCategoryIds.FailedErrorImports)));
        var second = await CopyCategory(MigrationCategoryIds.FailedErrorImports);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bodiesWritten, Is.EqualTo(1), "the large body is the only one the first copy writes to body storage");
            Assert.That(second.AlreadyPresent, Is.EqualTo(2), "--migration-retry re-reads from the start, and the rows the first copy wrote are left alone");
            Assert.That(await Target.Count(Category), Is.EqualTo(2));
            Assert.That(RecordedBodies.Written, Has.Count.EqualTo(bodiesWritten), "a body is written only for an import the target does not hold yet");
        }
    }

    [Test]
    public async Task Only_the_first_import_of_a_key_in_a_batch_has_its_body_written()
    {
        var first = LargeBody();
        var second = LargeBody();
        await SeedSource((DocumentId("native-8-a"), ImportOf("native-8", first)), (DocumentId("native-8-b"), ImportOf("native-8", second)));

        var result = await CopyCategory(MigrationCategoryIds.FailedErrorImports);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1), "both documents derive one key, and the insert keeps the first row of a key");
            Assert.That(RecordedBodies.Written.Select(written => written.Body), Is.EqualTo(new[] { first }), "a second body under the same id would replace the body of the row the insert kept");
        }
    }

    [Test]
    public async Task An_import_with_no_message_is_skipped_as_a_required_value_missing()
    {
        await SeedSource((DocumentId("native-5"), new FailedErrorImport { Id = "native-5", Message = null, ExceptionInfo = "Deserialization failed" }));
        await SeedSourceFailedErrorImport("native-6", [6], "Deserialization failed");

        var result = await CopyCategory(MigrationCategoryIds.FailedErrorImports);
        Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "one fault skip ends a required category Failed");

        var checkpoint = await ReadCheckpoint(MigrationCategoryIds.FailedErrorImports);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(1), "a skip copies the rest of the batch, where a throw would have stopped it");
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(1));
            Assert.That(checkpoint.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 1 }),
                "FailedErrorImports.MessageId is NOT NULL; the source attaches an empty body, so nothing is skipped as BodyMissing or BodyUnreadable");
        }
    }

    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.FailedErrorImports)!;

    static string DocumentId(string nativeMessageId) => $"FailedErrorImports/{nativeMessageId}";

    static Dictionary<string, string> HeadersFor(string nativeMessageId) => new() { ["NServiceBus.MessageId"] = nativeMessageId };

    // One byte over the limit, because the store moves a body out only when it is larger than the limit.
    byte[] LargeBody()
    {
        var body = new byte[EFSettings.BodyStorage.MaxBodySizeToStore + 1];
        Random.Shared.NextBytes(body);
        return body;
    }

    Task SeedSourceFailedErrorImport(string nativeMessageId, byte[] body, string exceptionInfo) =>
        SeedSource((DocumentId(nativeMessageId), ImportOf(nativeMessageId, body, exceptionInfo)));

    static FailedErrorImport ImportOf(string nativeMessageId, byte[] body, string exceptionInfo = "Deserialization failed") => new()
    {
        Id = nativeMessageId,
        Message = new FailedTransportMessage { Id = nativeMessageId, Headers = HeadersFor(nativeMessageId), Body = body },
        ExceptionInfo = exceptionInfo
    };

    async Task<DateTime> SourceLastModified(string nativeMessageId)
    {
        using var session = SourceStore.OpenAsyncSession(Source.PrimaryDatabase);

        return session.Advanced.GetLastModifiedFor(await session.LoadAsync<FailedErrorImport>(DocumentId(nativeMessageId)))!.Value.ToUniversalTime();
    }

    Task<FailedErrorImportEntity> GetImport(string nativeMessageId)
    {
        var key = FailedErrorImport.DeriveKey(HeadersFor(nativeMessageId), nativeMessageId);

        return QueryTarget(dbContext => dbContext.FailedErrorImports.AsNoTracking().SingleAsync(row => row.UniqueMessageId == key));
    }
}
