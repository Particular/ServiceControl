namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class FailedMessagesMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    IMigrationCheckpointStore CheckpointStore => ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();

    IBodyStoragePersistence BodyStore => ServiceProvider.GetRequiredService<IBodyStoragePersistence>();

    static readonly MigrationBody SmallTextBody = new(Encoding.UTF8.GetBytes("<order>1</order>"), "text/xml");

    static readonly MigrationCategory FailedMessagesCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

    [Test]
    public async Task A_copied_message_arrives_with_its_failure_group_rows()
    {
        var failure = new IngestedFailure();

        var result = await Target.Write(FailedMessagesCategory, BatchOf(failure.ToFailedMessage()), Checkpoint());

        var groups = await Query(db => db.FailedMessageGroups.AsNoTracking().Where(group => group.FailedMessageUniqueId == failure.UniqueMessageId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(groups.Select(group => (group.GroupId, group.Title, group.Type)), Is.EqualTo(failure.Groups.Select(group => (group.Id, group.Title, group.Type))));
        }
    }

    [Test]
    public async Task A_message_already_in_the_target_keeps_its_row_and_gets_no_group_rows()
    {
        var failure = new IngestedFailure();
        await Query(db =>
        {
            db.FailedMessages.Add(new FailedMessageEntity { UniqueMessageId = failure.UniqueMessageId, Status = FailedMessageStatus.Unresolved, ExceptionMessage = "Already in the target", HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        var result = await Target.Write(FailedMessagesCategory, BatchOf(failure.ToFailedMessage()), Checkpoint());

        var row = await Query(db => db.FailedMessages.AsNoTracking().SingleAsync(message => message.UniqueMessageId == failure.UniqueMessageId));
        var groups = await Query(db => db.FailedMessageGroups.AsNoTracking().CountAsync(group => group.FailedMessageUniqueId == failure.UniqueMessageId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero, "nothing was lost, so a skip would push a healthy category toward the halt threshold");
            Assert.That(row.ExceptionMessage, Is.EqualTo("Already in the target"), "insert-if-absent never updates");
            Assert.That(groups, Is.Zero, "the copy wrote the source's groups onto a message it did not insert");
        }
    }

    [Test]
    public async Task Copied_counts_the_messages_inserted_not_the_messages_handed_in()
    {
        var present = new IngestedFailure();
        var absent = new IngestedFailure();
        await Target.Write(FailedMessagesCategory, BatchOf(present.ToFailedMessage()), Checkpoint());
        var saved = await CheckpointStore.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var result = await Target.Write(FailedMessagesCategory, BatchOf(present.ToFailedMessage(), absent.ToFailedMessage()), saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Two_documents_sharing_a_unique_message_id_store_only_the_first_documents_groups()
    {
        var first = new IngestedFailure { Groups = [new() { Id = "group-of-the-first", Title = "First", Type = "Message Type" }] };
        var second = new IngestedFailure { Groups = [new() { Id = "group-of-the-second", Title = "Second", Type = "Message Type" }] };
        var secondDocument = second.ToFailedMessage();
        secondDocument.UniqueMessageId = first.UniqueMessageIdString;

        var result = await Target.Write(FailedMessagesCategory, BatchOf(first.ToFailedMessage(), secondDocument), Checkpoint());

        var groupIds = await Query(db => db.FailedMessageGroups.AsNoTracking().Where(group => group.FailedMessageUniqueId == first.UniqueMessageId).Select(group => group.GroupId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(groupIds, Is.EqualTo(new[] { "group-of-the-first" }), "the declined document's groups landed on the message the insert kept");
        }
    }

    [Test]
    public async Task A_document_the_mapper_skips_is_counted_under_its_reason_and_never_written()
    {
        var good = new IngestedFailure();
        var unparseable = new IngestedFailure().ToFailedMessage();
        unparseable.UniqueMessageId = "not-a-guid";
        var attemptless = new IngestedFailure().ToFailedMessage();
        attemptless.ProcessingAttempts.Clear();

        var result = await Target.Write(FailedMessagesCategory, BatchOf(good.ToFailedMessage(), unparseable, attemptless), Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.EqualTo(2));
            Assert.That(result.AlreadyPresent, Is.Zero);
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.UnparseableUniqueMessageId] = 1,
                [MigrationSkipReason.NoProcessingAttempts] = 1
            }));
            Assert.That(await Query(db => db.FailedMessages.CountAsync()), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task A_document_with_a_value_of_the_wrong_type_or_no_failure_details_is_skipped_and_the_rest_copy()
    {
        var good = new IngestedFailure();
        var badTimeSent = new IngestedFailure().ToFailedMessage();
        badTimeSent.ProcessingAttempts[^1].MessageMetadata["TimeSent"] = "garbage";
        var noFailureDetails = new IngestedFailure().ToFailedMessage();
        noFailureDetails.ProcessingAttempts[^1].FailureDetails = null;

        var result = await Target.Write(FailedMessagesCategory, BatchOf(badTimeSent, good.ToFailedMessage(), noFailureDetails), Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EquivalentTo(new[] { $"FailedMessages/{badTimeSent.UniqueMessageId}", $"FailedMessages/{noFailureDetails.UniqueMessageId}" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.MalformedValue] = 1,
                [MigrationSkipReason.RequiredValueMissing] = 1
            }), "one malformed document no longer halts the category");
            Assert.That(await Query(db => db.FailedMessages.AsNoTracking().Select(message => message.UniqueMessageId).ToListAsync()), Is.EqualTo(new[] { good.UniqueMessageId }));
        }
    }

    [Test]
    public async Task A_message_or_group_value_longer_than_its_column_skips_the_message_whole_and_the_rest_copy()
    {
        var fitsExactly = new IngestedFailure
        {
            ConversationId = new string('c', 450),
            Groups = [new() { Id = new string('g', 64), Title = "OrderPlaced", Type = new string('t', 255) }]
        };
        var longConversation = new IngestedFailure { ConversationId = new string('c', 451) };
        var longGroupType = new IngestedFailure { Groups = [new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = new string('t', 256) }] };
        var longMessageType = new IngestedFailure { MessageType = new string('m', 600) };
        var batch = BatchOf(fitsExactly.ToFailedMessage(), longConversation.ToFailedMessage(), longGroupType.ToFailedMessage(), longMessageType.ToFailedMessage());

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new FailedMessagesWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>(), BodyStore, ServiceProvider.GetRequiredService<EFPersisterSettings>()), batch);
        var result = await Target.Write(FailedMessagesCategory, batch, Checkpoint());

        var copiedIds = await Query(db => db.FailedMessages.AsNoTracking().Select(message => message.UniqueMessageId).ToListAsync());
        var groupOwners = await Query(db => db.FailedMessageGroups.AsNoTracking().Select(group => group.FailedMessageUniqueId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2), "values exactly as long as their columns fit, and MessageType keeps its last 450 characters as ingestion does, so neither is skipped for length");
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { $"FailedMessages/{longConversation.UniqueMessageIdString}", $"FailedMessages/{longGroupType.UniqueMessageIdString}" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 2 }), "an over-long value would fail the whole insert and halt the category");
            Assert.That(copiedIds, Is.EquivalentTo(new[] { fitsExactly.UniqueMessageId, longMessageType.UniqueMessageId }));
            Assert.That(groupOwners, Is.EquivalentTo(new[] { fitsExactly.UniqueMessageId, longMessageType.UniqueMessageId }), "a message whose group row cannot be stored is skipped whole, not copied without its group");
            Assert.That(details, Is.EqualTo(new[]
            {
                $"{PreparedSkips.TableAndColumnOf<FailedMessageEntity>(ServiceProvider, nameof(FailedMessageEntity.ConversationId))} takes at most 450 characters and the value holds 451, on document 'FailedMessages/{longConversation.UniqueMessageIdString}'",
                $"{PreparedSkips.TableAndColumnOf<FailedMessageGroupEntity>(ServiceProvider, nameof(FailedMessageGroupEntity.Type))} takes at most 255 characters and the value holds 256, on document 'FailedMessages/{longGroupType.UniqueMessageIdString}'"
            }));
        }
    }

    [Test]
    public async Task Count_takes_in_only_unresolved_and_retry_issued_messages()
    {
        await Target.Write(FailedMessagesCategory, BatchOf(new IngestedFailure().ToFailedMessage(), new IngestedFailure().ToFailedMessage(FailedMessageStatus.RetryIssued)), Checkpoint());
        await Query(db =>
        {
            db.FailedMessages.AddRange(
                new FailedMessageEntity { UniqueMessageId = Guid.NewGuid(), Status = FailedMessageStatus.Archived, HeadersJson = "{}", FailingEndpointAddress = "error" },
                new FailedMessageEntity { UniqueMessageId = Guid.NewGuid(), Status = FailedMessageStatus.Resolved, HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Target.Count(FailedMessagesCategory), Is.EqualTo(2), "the archived and resolved rows belong to another category");
            Assert.That(await Query(db => db.FailedMessages.CountAsync()), Is.EqualTo(4), "the filter is doing the work, not an empty table");
        }
    }

    // PostgreSQL takes a fixed number of rows per statement whatever the entity, so only SQL Server can catch the wrong entity here.
    [Test]
    public async Task A_batch_is_one_statement_of_failed_message_rows()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var expected = await Query(db => Task.FromResult(dialect.RowsPerStatement(MigrationInsert<FailedMessageEntity>.For(db).Columns.Count)));

        Assert.That(await Target.BatchSizeFor(FailedMessagesCategory), Is.EqualTo(expected));
    }

    [Test]
    public async Task A_write_larger_than_one_statement_copies_every_message_and_its_groups()
    {
        var rowsPerStatement = await Target.BatchSizeFor(FailedMessagesCategory);
        var documents = Enumerable.Range(0, (rowsPerStatement * 2) + 1).Select(_ => new IngestedFailure().ToFailedMessage()).ToArray();

        var result = await Target.Write(FailedMessagesCategory, BatchOf(documents), Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(documents.Length));
            Assert.That(await Query(db => db.FailedMessages.CountAsync()), Is.EqualTo(documents.Length));
            Assert.That(await Query(db => db.FailedMessageGroups.CountAsync()), Is.EqualTo(documents.Length));
        }
    }

    [Test]
    public async Task A_message_already_in_the_target_has_no_body_written_and_one_it_lacks_does()
    {
        var present = new IngestedFailure { ContentType = "application/octet-stream" };
        var absent = new IngestedFailure { ContentType = "application/octet-stream" };
        var binaryBody = new MigrationBody(new byte[] { 0x08, 0x00, 0x96, 0x01 }, "application/octet-stream");
        await Query(db =>
        {
            db.FailedMessages.Add(new FailedMessageEntity { UniqueMessageId = present.UniqueMessageId, Status = FailedMessageStatus.Unresolved, HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        var result = await Target.Write(FailedMessagesCategory, BatchOf(RowOf(present.ToFailedMessage(), binaryBody), RowOf(absent.ToFailedMessage(), binaryBody)), Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(await BodyStored(present.UniqueMessageIdString), Is.False, "the insert declines a message the target holds, so a body written for it is a wasted write");
            Assert.That(await BodyStored(absent.UniqueMessageIdString), Is.True, "the test proves nothing if no body is ever written");
        }
    }

    [Test]
    public async Task Only_the_first_document_sharing_a_unique_message_id_has_its_body_stored()
    {
        var first = new IngestedFailure();
        var second = new IngestedFailure { ContentType = "application/octet-stream" };
        var secondDocument = second.ToFailedMessage();
        secondDocument.UniqueMessageId = first.UniqueMessageIdString;

        var result = await Target.Write(FailedMessagesCategory, BatchOf(
            RowOf(first.ToFailedMessage(), SmallTextBody),
            RowOf(secondDocument, new MigrationBody(new byte[] { 0x08, 0x00, 0x96, 0x01 }, "application/octet-stream"))), Checkpoint());

        var row = await Query(db => db.FailedMessages.AsNoTracking().SingleAsync(message => message.UniqueMessageId == first.UniqueMessageId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(row.BodyStoredExternally, Is.False, "the insert keeps the first document, whose body is inline");
            Assert.That(await BodyStored(first.UniqueMessageIdString), Is.False, "the declined document's body was stored under the id of the row the insert kept");
        }
    }

    [Test]
    public async Task A_body_content_type_longer_than_its_column_skips_the_message_and_the_rest_copy()
    {
        var good = new IngestedFailure();
        var longContentType = new IngestedFailure().ToFailedMessage();

        var result = await Target.Write(FailedMessagesCategory, BatchOf(
            RowOf(good.ToFailedMessage(), SmallTextBody),
            RowOf(longContentType, SmallTextBody with { ContentType = $"text/{new string('x', 446)}" })), Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { $"FailedMessages/{longContentType.UniqueMessageId}" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 1 }), "an over-long content type would fail the whole insert and halt the category");
        }
    }

    [Test]
    public async Task A_body_that_cannot_be_written_fails_the_write_before_any_row_or_checkpoint_commits()
    {
        var target = new EFCoreMigrationTarget(
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            ServiceProvider.GetRequiredService<IMigrationSqlDialect>(),
            new UnwritableBodyStore(),
            ServiceProvider.GetRequiredService<EFPersisterSettings>(),
            ServiceProvider.GetRequiredService<ILogger<EFCoreMigrationTarget>>());
        var inline = new IngestedFailure();
        var binary = new IngestedFailure { ContentType = "application/octet-stream" };

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => target.Write(FailedMessagesCategory, BatchOf(
            RowOf(inline.ToFailedMessage(), SmallTextBody),
            RowOf(binary.ToFailedMessage(), new MigrationBody(new byte[] { 0x08, 0x00, 0x96, 0x01 }, "application/octet-stream"))), Checkpoint()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"'FailedMessages/{binary.UniqueMessageId}'").And.Contain($"'{binary.UniqueMessageId}'").And.Contain(nameof(IOException)),
                "a storage SDK's own error does not name the body, so without the row and body id the operator cannot tell which message halted the category");
            Assert.That(exception.InnerException, Is.TypeOf<IOException>().And.Message.EqualTo(UnwritableBodyStore.Failure));
            Assert.That(await Query(db => db.FailedMessages.CountAsync()), Is.Zero, "a row committed without its body would name a body that was never stored");
            Assert.That(await CheckpointStore.Read(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages), Is.Null, "a checkpoint past the batch would stop a retry from reading it again");
        }
    }

    [Test]
    public void A_document_that_cannot_be_prepared_fails_the_write_naming_the_row_and_category()
    {
        // A content type header stored with a null value is found by the classifier and then throws.
        var document = new IngestedFailure { ContentType = null }.ToFailedMessage();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(FailedMessagesCategory, BatchOf(document), Checkpoint()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"'FailedMessages/{document.UniqueMessageId}'").And.Contain(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages).And.Contain(nameof(NullReferenceException)),
                "the halt's LastError is built from this message, and without the row the operator cannot tell which document to look at");
            Assert.That(exception.InnerException, Is.TypeOf<NullReferenceException>());
        }
    }

    [Test]
    public void A_row_that_reaches_the_writer_without_a_body_fails_the_write_naming_the_row_and_category()
    {
        var document = new IngestedFailure().ToFailedMessage();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(FailedMessagesCategory, BatchOf(RowOf(document, body: null)), Checkpoint()));

        Assert.That(exception.Message, Does.Contain($"'FailedMessages/{document.UniqueMessageId}'").And.Contain(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages));
    }

    // Disposes the stream, because the file store holds the body file open until it is.
    async Task<bool> BodyStored(string bodyId)
    {
        var stored = await BodyStore.ReadBody(bodyId);
        if (stored is null)
        {
            return false;
        }

        await stored.Stream.DisposeAsync();
        return true;
    }

    async Task<T> Query<T>(Func<ServiceControlDbContext, Task<T>> query)
    {
        using var scope = ServiceProvider.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>());
    }

    static MigrationBatch BatchOf(params FailedMessage[] documents) => BatchOf([.. documents.Select(document => RowOf(document, SmallTextBody))]);

    static MigrationBatch BatchOf(params MigrationRow[] rows) => new(rows, rows[^1].SourceId);

    // Every row carries the two dates the source resolves, as a real source's rows do.
    static MigrationRow RowOf(FailedMessage document, MigrationBody body) =>
        new($"FailedMessages/{document.UniqueMessageId}", document, new Dictionary<string, object>
        {
            [MigrationMetadataKeys.StatusChangedAt] = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            [MigrationMetadataKeys.LastModified] = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)
        }, body);

    sealed class UnwritableBodyStore : IBodyStoragePersistence
    {
        public const string Failure = "The body store is unreachable";

        public Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default) => Task.FromException(new IOException(Failure));

        public Task<MessageBodyFileResult> ReadBody(string bodyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteBodyIfExists(string bodyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    static MigrationCheckpoint Checkpoint() =>
        new(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null);
}
