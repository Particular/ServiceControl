namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.Configuration;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.DataMigration;

[TestFixture]
class UnresolvedFailedMessageSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_both_unresolved_statuses_and_neither_of_the_others()
    {
        var unresolved = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));
        var retryIssued = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.RetryIssued));
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived));
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Resolved));
        await CompleteDatabaseOperation();

        var rows = await ReadAll();

        Assert.That(
            rows.Select(row => ((FailedMessage)row.Document).UniqueMessageId),
            Is.EquivalentTo(new[] { unresolved.UniqueMessageId, retryIssued.UniqueMessageId }));
    }

    [Test]
    public async Task Counts_only_the_documents_this_category_copies_not_the_whole_collection()
    {
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived));
        await CompleteDatabaseOperation();

        await using var source = await OpenMigrationSource();

        Assert.That(await source.Count(Category), Is.EqualTo(2), "the archived message shares the collection but belongs to another category");
    }

    [Test]
    public async Task Refuses_a_document_whose_status_neither_failed_message_category_copies()
    {
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage((FailedMessageStatus)99));
        await CompleteDatabaseOperation();

        var refusal = Assert.ThrowsAsync<InvalidOperationException>(ReadAll);

        Assert.That(refusal.Message, Does.Contain(stored.Id).And.Contain("99"), "a status no category copies would otherwise be left behind with nothing to show for it");
    }

    [Test]
    public async Task Hands_over_attempt_times_as_UTC()
    {
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(numberOfAttempts: 2));
        await CompleteDatabaseOperation();

        var attempts = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts;

        Assert.That(attempts.SelectMany(attempt => new[] { attempt.AttemptedAt.Kind, attempt.FailureDetails.TimeOfFailure.Kind }), Is.All.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public async Task Resolves_StatusChangedAt_to_the_newest_attempt_when_it_is_neither_first_nor_last()
    {
        var failure = new IngestedFailure();
        var message = failure.ToFailedMessage(FailedMessageStatus.Unresolved, numberOfAttempts: 3);
        var attempts = message.ProcessingAttempts;
        message.ProcessingAttempts = [attempts[1], attempts[2], attempts[0]];
        await SeedFailedMessage(message);
        await CompleteDatabaseOperation();

        var row = (await ReadAll()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Metadata[MigrationMetadataKeys.StatusChangedAt], Is.EqualTo(failure.AttemptedAt));
            Assert.That(((DateTime)row.Metadata[MigrationMetadataKeys.StatusChangedAt]).Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    [Test]
    public async Task Sends_only_the_migration_metadata_keys_across_the_seam()
    {
        await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));
        await CompleteDatabaseOperation();

        var row = (await ReadAll()).Single();

        Assert.That(row.Metadata.Keys, Is.EquivalentTo(new[] { MigrationMetadataKeys.StatusChangedAt, MigrationMetadataKeys.LastModified }), "a store's own metadata, such as @expires, never travels");
    }

    [Test]
    public async Task Carries_the_documents_own_last_modified()
    {
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));
        await CompleteDatabaseOperation();

        DateTime lastModified;

        using (var session = await SessionProvider.OpenSession())
        {
            lastModified = session.Advanced.GetLastModifiedFor(await session.LoadAsync<FailedMessage>(stored.Id)).Value;
        }

        var row = (await ReadAll()).Single();

        Assert.That(row.Metadata[MigrationMetadataKeys.LastModified], Is.EqualTo(lastModified.ToUniversalTime()), "the value RavenDB shows as the message's LastModified, not the copy time");
    }

    [Test]
    public async Task A_run_of_dropped_documents_yields_an_empty_batch_only_once_the_walk_limit_is_reached()
    {
        var ids = await SeedInIdOrder(Enumerable.Repeat(FailedMessageStatus.Archived, 7).ToArray());

        var batches = await StreamWithWalkLimit(batchSize: 2, maxDocumentsWalkedPerBatch: 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { ids[2], ids[5], ids[6] }), "an empty stretch commits once per walk limit, plus the tail so the cursor past it is saved");
            Assert.That(batches.SelectMany(batch => batch.Rows), Is.Empty);
        }
    }

    [Test]
    public async Task The_category_reads_no_archived_or_resolved_document()
    {
        await SeedInIdOrder(FailedMessageStatus.Archived, FailedMessageStatus.Resolved, FailedMessageStatus.Archived);

        await using var source = await OpenMigrationSource();
        var batches = await CollectBatches(source, Category, batchSize: 2);

        Assert.That(batches, Is.Empty, "the copy runs while ServiceControl is closed, so reading the archive only to drop it makes the outage grow with the archive");
    }

    [Test]
    public async Task Resuming_after_a_cursor_reads_only_the_unresolved_messages_after_it()
    {
        var ids = await SeedInIdOrder(
            FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.RetryIssued,
            FailedMessageStatus.Unresolved, FailedMessageStatus.Resolved, FailedMessageStatus.Unresolved);

        await using var source = await OpenMigrationSource();
        var first = (await CollectBatches(source, Category, batchSize: 2))[0];
        var resumed = await CollectBatches(source, Category, resumeAfter: first.Cursor, batchSize: 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Rows.Select(row => row.SourceId), Is.EqualTo(new[] { ids[0], ids[2] }));
            Assert.That(first.Cursor, Is.EqualTo(ids[2]));
            Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.EqualTo(new[] { ids[3], ids[5] }), "a resume neither repeats a row nor skips one");
        }
    }

    [Test]
    public async Task Resuming_after_a_cursor_this_source_never_issued_refuses_instead_of_starting_past_it()
    {
        await SeedInIdOrder(FailedMessageStatus.Unresolved, FailedMessageStatus.Unresolved);
        const string cursor = "FailedMessages/zzz-from-another-database";

        await using var source = await OpenMigrationSource();
        var refusal = Assert.ThrowsAsync<InvalidOperationException>(() => CollectBatches(source, Category, resumeAfter: cursor));

        Assert.That(refusal.Message, Does.Contain(Category.Id).And.Contain(cursor), "a cursor from a restored backup or another database would otherwise resume part way through and skip rows");
    }

    [Test]
    public async Task Waits_for_the_index_to_catch_up_so_no_unresolved_message_is_missed()
    {
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived));
        await CompleteDatabaseOperation();
        await DocumentStore.Maintenance.SendAsync(new StopIndexOperation(IndexName), TestContext.CurrentContext.CancellationToken);

        // The stopped index still counts the message as archived, so its count matches the collection, and only the wait for the index keeps the read from finding nothing.
        using (var session = DocumentStore.OpenAsyncSession())
        {
            (await session.LoadAsync<FailedMessage>(stored.Id, TestContext.CurrentContext.CancellationToken)).Status = FailedMessageStatus.Unresolved;
            await session.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
        }

        var reading = ReadAll();
        var finishedWhileStale = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(3), TestContext.CurrentContext.CancellationToken)) == reading;

        await DocumentStore.Maintenance.SendAsync(new StartIndexOperation(IndexName), TestContext.CurrentContext.CancellationToken);
        var rows = await reading.WaitAsync(TimeSpan.FromSeconds(60), TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finishedWhileStale, Is.False, "a query on an index that has not caught up leaves out the messages it has not indexed yet");
            Assert.That(rows.Select(row => row.SourceId), Is.EqualTo(new[] { stored.Id }));
        }
    }

    [Test]
    public async Task Resumes_after_a_cursor_that_names_an_archived_message()
    {
        var ids = await SeedInIdOrder(
            FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Unresolved);

        await using var source = await OpenMigrationSource();
        var resumed = await CollectBatches(source, Category, resumeAfter: ids[1]);

        Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.EqualTo(new[] { ids[2], ids[4] }), "a run that read the whole collection can save an archived message as its cursor, and the next run may query the index");
    }

    [Test]
    public async Task Reads_through_a_Corax_index_as_through_a_Lucene_one()
    {
        await UseCorax();
        var ids = await SeedInIdOrder(FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.RetryIssued, FailedMessageStatus.Resolved);

        await using var source = await OpenMigrationSource();
        var batches = await CollectBatches(source, Category);

        Assert.That(batches.Select(batch => (batch.Cursor, batch.Rows.Select(row => row.SourceId))), Is.EqualTo(new[] { (ids[2], new[] { ids[0], ids[2] }) }), "a full read would walk on to the resolved message and save its cursor past it");
    }

    [Test]
    public async Task Resumes_through_a_Corax_index_with_no_row_repeated_or_skipped()
    {
        await UseCorax();
        var statuses = Enumerable.Range(0, 60).Select(position => (FailedMessageStatus)((position % 4) + 1)).ToArray();
        var ids = await SeedInIdOrder(statuses);
        var expected = ids.Where((_, position) => statuses[position] is FailedMessageStatus.Unresolved or FailedMessageStatus.RetryIssued).ToList();

        await using var source = await OpenMigrationSource();
        var read = new List<string>();
        string cursor = null;

        // Every batch is read by a fresh call, as a restart after each commit would.
        while ((await CollectBatches(source, Category, resumeAfter: cursor, batchSize: 7)).FirstOrDefault() is { } batch)
        {
            Assert.That(batch.Cursor, Is.Not.EqualTo(cursor), "a resume that hands back the batch it resumed after never ends");
            read.AddRange(batch.Rows.Select(row => row.SourceId));
            cursor = batch.Cursor;
        }

        var afterArchived = await CollectBatches(source, Category, resumeAfter: ids[Array.IndexOf(statuses, FailedMessageStatus.Archived)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read, Is.EqualTo(expected), "Corax has to order and compare id() as the cursor expects");
            Assert.That(afterArchived.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.EqualTo(expected.SkipWhile(id => string.CompareOrdinal(id, ids[Array.IndexOf(statuses, FailedMessageStatus.Archived)]) < 0)));
        }
    }

    [Test]
    public async Task Reads_the_whole_collection_when_the_index_is_missing()
    {
        await DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation(IndexName), TestContext.CurrentContext.CancellationToken);
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));

        var rows = await ReadAll().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CurrentContext.CancellationToken);

        Assert.That(rows.Select(row => row.SourceId), Is.EqualTo(new[] { stored.Id }), "the copy cannot build the index on a source it only reads, so reading everything is the only way through");
    }

    [Test]
    public async Task Reads_the_whole_collection_instead_of_waiting_for_a_disabled_index()
    {
        await DocumentStore.Maintenance.SendAsync(new DisableIndexOperation(IndexName), TestContext.CurrentContext.CancellationToken);
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));

        var rows = await ReadAll().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CurrentContext.CancellationToken);

        Assert.That(rows.Select(row => row.SourceId), Is.EqualTo(new[] { stored.Id }), "a disabled index never catches up, so waiting for it would keep ServiceControl closed for good");
    }

    [Test]
    public async Task Reads_the_whole_collection_instead_of_waiting_for_an_errored_index()
    {
        var unindexable = await SeedFailedMessage(FailedMessageWithATimeSentThatIsNotADate());
        await CompleteDatabaseOperation();
        Assert.That(await IndexStateNow(), Is.EqualTo(IndexState.Error), "one document that fails out of one attempt errors the index");

        // An errored index stops part way, so this one stays unindexed and the index stays stale.
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved));

        var rows = await ReadAll().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CurrentContext.CancellationToken);

        Assert.That(rows.Select(row => row.SourceId), Is.EquivalentTo(new[] { unindexable.Id, stored.Id }), "an errored index never catches up, so waiting for it would keep ServiceControl closed for good");
    }

    [Test]
    public async Task Reads_the_whole_collection_when_the_index_leaves_out_a_document_it_could_not_index()
    {
        await using (var bulkInsert = DocumentStore.BulkInsert(token: TestContext.CurrentContext.CancellationToken))
        {
            foreach (var message in Enumerable.Range(0, 200).Select(_ => new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived)))
            {
                await bulkInsert.StoreAsync(message, FailedMessageIdGenerator.MakeDocumentId(message.UniqueMessageId));
            }
        }

        var unindexable = await SeedFailedMessage(FailedMessageWithATimeSentThatIsNotADate());
        await CompleteDatabaseOperation();
        Assert.That(await IndexStateNow(), Is.EqualTo(IndexState.Normal), "RavenDB errors the whole index only above a 15 percent failure rate, so one bad document among many leaves it working and silently short");

        var rows = await ReadAll();

        Assert.That(rows.Select(row => row.SourceId), Is.EqualTo(new[] { unindexable.Id }), "a query would leave out the document the index could not map, where the target should record it as a skip");
    }

    [Test]
    public async Task A_batch_yields_as_soon_as_it_holds_batch_size_rows_however_far_below_the_walk_limit()
    {
        var ids = await SeedInIdOrder(Enumerable.Repeat(FailedMessageStatus.Unresolved, 5).ToArray());

        var batches = await StreamWithWalkLimit(batchSize: 2, maxDocumentsWalkedPerBatch: 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { ids[1], ids[3], ids[4] }));
            Assert.That(batches.Select(batch => batch.Rows.Count), Is.EqualTo(new[] { 2, 2, 1 }), "a batch never holds more than the target can write in one go");
        }
    }

    [Test]
    public async Task A_batch_short_of_batch_size_rows_yields_once_the_walk_limit_is_reached()
    {
        var ids = await SeedInIdOrder(
            FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Archived, FailedMessageStatus.Archived,
            FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Archived);

        var batches = await StreamWithWalkLimit(batchSize: 2, maxDocumentsWalkedPerBatch: 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { ids[3], ids[6] }), "a sparse stretch still saves its cursor every walk limit, so a resume walks at most that far again");
            Assert.That(batches.Select(batch => batch.Rows.Select(row => row.SourceId)), Is.EqualTo(new[] { new[] { ids[0] }, new[] { ids[4] } }));
        }
    }

    [Test]
    public async Task A_full_batch_yields_on_rows_held_not_on_documents_walked()
    {
        var ids = await SeedInIdOrder(
            FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, FailedMessageStatus.Unresolved);

        var batches = await StreamWithWalkLimit(batchSize: 2, maxDocumentsWalkedPerBatch: 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { ids[2], ids[4] }), "a batch is full when it holds batch size rows, however many documents it walked to find them");
            Assert.That(batches.Select(batch => batch.Rows.Select(row => row.SourceId)), Is.EqualTo(new[] { new[] { ids[0], ids[2] }, new[] { ids[4] } }));
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Refuses_a_walk_limit_that_is_not_positive(int maxDocumentsWalkedPerBatch)
    {
        var refusal = Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => StreamWithWalkLimit(batchSize: 2, maxDocumentsWalkedPerBatch));

        Assert.That(refusal.ParamName, Is.EqualTo("maxDocumentsWalkedPerBatch"), "a limit of zero or less is never reached, so every batch would wait for batch size rows however far it walked");
    }

    [Test]
    public async Task The_category_saves_its_cursor_at_least_every_ten_thousand_documents_read()
    {
        var messages = Enumerable.Range(0, 10_001)
            .Select(_ => new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved))
            .OrderBy(message => FailedMessageIdGenerator.MakeDocumentId(message.UniqueMessageId), StringComparer.Ordinal)
            .ToList();

        await using (var bulkInsert = DocumentStore.BulkInsert(token: TestContext.CurrentContext.CancellationToken))
        {
            foreach (var message in messages)
            {
                await bulkInsert.StoreAsync(message, FailedMessageIdGenerator.MakeDocumentId(message.UniqueMessageId));
            }
        }

        await CompleteDatabaseOperation();

        await using var source = await OpenMigrationSource();
        var batches = await CollectBatches(source, Category, batchSize: 20_000);

        Assert.That(
            batches.Select(batch => (batch.Cursor, batch.Rows.Count)),
            Is.EqualTo(new[] { (FailedMessageIdGenerator.MakeDocumentId(messages[9_999].UniqueMessageId), 10_000), (FailedMessageIdGenerator.MakeDocumentId(messages[10_000].UniqueMessageId), 1) }),
            "the reader commits every RavenDocumentStream.MaxDocumentsWalkedPerBatch documents, which is 10,000, however large the batch size");
    }

    [Test]
    public async Task A_document_stored_without_FailureDetails_comes_back_with_an_empty_failing_address()
    {
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage(numberOfAttempts: 2));
        await DocumentStore.Operations.SendAsync(new PatchOperation(stored.Id, null, new PatchRequest { Script = "delete this.ProcessingAttempts[0].FailureDetails; delete this.ProcessingAttempts[1].FailureDetails;" }));
        await CompleteDatabaseOperation();
        var readStartedAt = DateTime.UtcNow;

        var attempts = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempts.Select(attempt => attempt.FailureDetails?.AddressOfFailingEndpoint), Is.All.Empty, "a missing key reads back as the default object, not null, so the mapper has to treat an empty address as missing");
            Assert.That(attempts.Select(attempt => attempt.FailureDetails.TimeOfFailure), Is.All.GreaterThanOrEqualTo(readStartedAt), "the default object stamps the time it was read, so its time of failure is invented");
        }
    }

    [Test]
    public async Task A_FailureDetails_stored_without_its_TimeOfFailure_comes_back_with_no_time_rather_than_the_time_it_was_read()
    {
        var failure = new IngestedFailure { FailingEndpointAddress = "Sales@MACHINE" };
        var stored = await SeedFailedMessage(failure.ToFailedMessage(numberOfAttempts: 2));
        await DocumentStore.Operations.SendAsync(new PatchOperation(stored.Id, null, new PatchRequest { Script = "delete this.ProcessingAttempts[0].FailureDetails.TimeOfFailure; delete this.ProcessingAttempts[1].FailureDetails.TimeOfFailure;" }));
        await CompleteDatabaseOperation();

        var attempts = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempts[^1].FailureDetails.TimeOfFailure, Is.EqualTo(default(DateTime)), "the mapper skips a newest attempt with no time of failure, and the read time would copy it with a made-up one");
            Assert.That(attempts[0].FailureDetails.TimeOfFailure, Is.EqualTo(default(DateTime)), "the mapper leaves an older attempt with no time out of the first and last failure times");
            Assert.That(attempts.Select(attempt => attempt.FailureDetails.AddressOfFailingEndpoint), Is.All.EqualTo("Sales@MACHINE"), "the rest of FailureDetails is still read as stored");
            Assert.That(attempts.Select(attempt => attempt.FailureDetails.Exception?.Message), Is.All.EqualTo(failure.ExceptionMessage));
        }
    }

    [Test]
    public async Task A_FailureDetails_stored_as_null_comes_back_as_null()
    {
        var stored = await SeedFailedMessage(new IngestedFailure().ToFailedMessage());
        await DocumentStore.Operations.SendAsync(new PatchOperation(stored.Id, null, new PatchRequest { Script = "this.ProcessingAttempts[0].FailureDetails = null;" }));
        await CompleteDatabaseOperation();

        var attempt = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts.Single();

        Assert.That(attempt.FailureDetails, Is.Null, "the mapper skips an attempt with no FailureDetails, so one bad document must not halt the category");
    }

    [TestCase("SendingEndpoint", NServiceBus.Headers.OriginatingHostId)]
    [TestCase("ReceivingEndpoint", NServiceBus.Headers.HostId)]
    public void RestoreMetadataTypes_gives_an_endpoint_the_empty_host_id_when_its_host_id_header_is_null(string key, string hostIdHeader)
    {
        var document = SenderAndReceiverHeadersFailure();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers[hostIdHeader] = null;
        attempt.MessageMetadata[key] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(attempt.MessageMetadata[key], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(Guid.Empty), "a header present with no value reads as no host id, as ingestion stores it, so the message copies");
    }

    [Test]
    public void RestoreMetadataTypes_leaves_a_document_whose_attempt_list_is_null_for_the_mapper_to_skip()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts = null;

        Assert.DoesNotThrow(() => FailedMessageStream.RestoreMetadataTypes(document));
    }

    [Test]
    public async Task Reads_a_document_whose_attempt_list_is_null_with_no_StatusChangedAt()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts = null;
        await SeedFailedMessage(document);
        await CompleteDatabaseOperation();

        var row = (await ReadAll()).Single();

        Assert.That(row.Metadata[MigrationMetadataKeys.StatusChangedAt], Is.Null, "the mapper skips a message with no attempts, so the source passes it through rather than halting the category");
    }

    [Test]
    public async Task Hands_over_the_metadata_ingestion_reads_as_the_types_it_reads()
    {
        var failure = new IngestedFailure { IsSystemMessage = true };
        await SeedFailedMessage(failure.ToFailedMessage());
        await CompleteDatabaseOperation();

        var metadata = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts.Single().MessageMetadata;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata["TimeSent"], Is.TypeOf<DateTime>().And.EqualTo(failure.TimeSent));
            Assert.That(((DateTime)metadata["TimeSent"]).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(metadata["SendingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(failure.SendingEndpoint.HostId));
            Assert.That(metadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(failure.ReceivingEndpoint.HostId));
            Assert.That(metadata["MessageType"], Is.TypeOf<string>().And.EqualTo(failure.MessageType));
            Assert.That(metadata["ConversationId"], Is.TypeOf<string>().And.EqualTo(failure.ConversationId));
            Assert.That(metadata["IsSystemMessage"], Is.TypeOf<bool>().And.True, "the mapper reads it with 'is true', so any other type reads as false");
        }
    }

    [Test]
    public async Task Restores_a_TimeSent_stored_as_text_and_an_endpoint_stored_without_its_type()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var stored = document.ProcessingAttempts.Single();
        stored.Headers["NServiceBus.ProcessingMachine"] = "MACHINE";
        stored.MessageMetadata["TimeSent"] = "2026-07-22T09:59:00.0000000Z";
        stored.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };
        await SeedFailedMessage(document);
        await CompleteDatabaseOperation();

        var attempt = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempt.MessageMetadata["TimeSent"], Is.TypeOf<DateTime>().And.EqualTo(new DateTime(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc)));
            Assert.That(((DateTime)attempt.MessageMetadata["TimeSent"]).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(attempt.MessageMetadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.Name)).EqualTo("Sales"), "rebuilt from the headers, not converted from the stored dictionary");
            Assert.That(((EndpointDetails)attempt.MessageMetadata["ReceivingEndpoint"]).Host, Is.EqualTo("MACHINE"));
        }
    }

    [Test]
    public async Task Passes_a_TimeSent_that_is_not_a_date_through_as_stored_and_gives_endpoints_whose_host_id_is_not_a_GUID_the_empty_host_id()
    {
        var document = SenderAndReceiverHeadersFailure();
        var stored = document.ProcessingAttempts.Single();
        stored.Headers[NServiceBus.Headers.HostId] = "not-a-guid";
        stored.Headers[NServiceBus.Headers.OriginatingHostId] = "not-a-guid-either";
        stored.MessageMetadata["TimeSent"] = "garbage";
        stored.MessageMetadata["SendingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored sender" };
        stored.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored receiver" };
        await SeedFailedMessage(document);
        await CompleteDatabaseOperation();

        var metadata = ((FailedMessage)(await ReadAll()).Single().Document).ProcessingAttempts.Single().MessageMetadata;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata["TimeSent"], Is.EqualTo("garbage"), "the target skips the document as malformed, so one bad document does not halt the category");
            Assert.That(metadata["SendingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(Guid.Empty));
            Assert.That(metadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(Guid.Empty));
        }
    }

    [Test]
    public void RestoreMetadataTypes_parses_text_as_UTC_and_rebuilds_untyped_endpoints_from_the_headers()
    {
        var document = new IngestedFailure().ToFailedMessage(numberOfAttempts: 2);
        var sendingHostId = Guid.NewGuid();

        foreach (var attempt in document.ProcessingAttempts)
        {
            attempt.Headers["NServiceBus.ProcessingMachine"] = "MACHINE";
            attempt.MessageMetadata["TimeSent"] = "2026-07-22T09:59:00.1234567";
            attempt.Headers[NServiceBus.Headers.OriginatingEndpoint] = "Ordering";
            attempt.Headers["NServiceBus.OriginatingMachine"] = "SENDER";
            attempt.Headers[NServiceBus.Headers.OriginatingHostId] = sendingHostId.ToString();
            attempt.MessageMetadata["SendingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };
            attempt.MessageMetadata["ReceivingEndpoint"] = "not an endpoint";
        }

        FailedMessageStream.RestoreMetadataTypes(document);

        var newest = document.ProcessingAttempts[^1];
        var older = document.ProcessingAttempts[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(newest.MessageMetadata["TimeSent"], Is.TypeOf<DateTime>().And.EqualTo(new DateTime(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc).AddTicks(1234567)));
            Assert.That(((DateTime)newest.MessageMetadata["TimeSent"]).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(newest.MessageMetadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.Name)).EqualTo("Sales"));
            var sending = (EndpointDetails)newest.MessageMetadata["SendingEndpoint"];
            Assert.That(sending, Is.Not.Null);
            Assert.That(sending.Name, Is.EqualTo("Ordering"));
            Assert.That(sending.Host, Is.EqualTo("SENDER"));
            Assert.That(sending.HostId, Is.EqualTo(sendingHostId));
            Assert.That(older.MessageMetadata["TimeSent"], Is.EqualTo("2026-07-22T09:59:00.1234567"), "the mapper reads only the newest attempt, so an older one is left as stored");
        }
    }

    [Test]
    public void RestoreMetadataTypes_leaves_a_TimeSent_text_that_is_not_a_date_as_stored()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var metadata = document.ProcessingAttempts.Single().MessageMetadata;
        metadata["TimeSent"] = "garbage";

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(metadata["TimeSent"], Is.EqualTo("garbage"), "the target skips the document as malformed, so one bad document does not halt the category");
    }

    [TestCase("SendingEndpoint", NServiceBus.Headers.OriginatingHostId)]
    [TestCase("ReceivingEndpoint", NServiceBus.Headers.HostId)]
    public void RestoreMetadataTypes_gives_an_endpoint_the_empty_host_id_when_its_host_id_header_is_not_a_GUID(string key, string hostIdHeader)
    {
        var document = SenderAndReceiverHeadersFailure();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers[hostIdHeader] = "not-a-guid";
        attempt.MessageMetadata[key] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        var endpoint = (EndpointDetails)attempt.MessageMetadata[key];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.HostId, Is.EqualTo(Guid.Empty), "ingestion stores an unparseable host id as the empty GUID, so the message copies rather than being skipped");
            Assert.That(endpoint.Name, Is.EqualTo(key == "SendingEndpoint" ? "Ordering" : "Sales"));
            Assert.That(endpoint.Host, Is.EqualTo(key == "SendingEndpoint" ? "SENDER" : "MACHINE"));
        }
    }

    [Test]
    public void RestoreMetadataTypes_builds_a_receiving_endpoint_from_the_failed_queue_when_the_headers_name_none()
    {
        var document = new IngestedFailure { QueueAddress = "Sales@MACHINE" }.ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers.Remove(NServiceBus.Headers.ProcessingEndpoint);
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        var endpoint = (EndpointDetails)attempt.MessageMetadata["ReceivingEndpoint"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.Name, Is.EqualTo("Sales"));
            Assert.That(endpoint.Host, Is.EqualTo("MACHINE"));
            Assert.That(endpoint.HostId, Is.EqualTo(new Guid("f0c10bfe-aba7-6ceb-d333-ab938515451c")), "ingestion derives the host id from the name and host when it has none");
        }
    }

    [Test]
    public void RestoreMetadataTypes_gives_a_receiving_endpoint_built_from_a_failed_queue_with_no_machine_the_unknown_host()
    {
        var document = new IngestedFailure { QueueAddress = "Sales" }.ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers.Remove(NServiceBus.Headers.ProcessingEndpoint);
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        var endpoint = (EndpointDetails)attempt.MessageMetadata["ReceivingEndpoint"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.Name, Is.EqualTo("Sales"));
            Assert.That(endpoint.Host, Is.EqualTo("unknown"));
            Assert.That(endpoint.HostId, Is.EqualTo(new Guid("862257db-407e-ec11-92cc-05ab0c3f7325")));
        }
    }

    [Test]
    public void RestoreMetadataTypes_leaves_a_newest_attempt_with_no_metadata_for_the_mapper_to_skip()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.MessageMetadata = null;

        Assert.DoesNotThrow(() => FailedMessageStream.RestoreMetadataTypes(document));
        Assert.That(attempt.MessageMetadata, Is.Null, "the mapper skips an attempt with no metadata, so an empty stand-in would hide the fault");
    }

    [Test]
    public void RestoreMetadataTypes_leaves_an_untyped_endpoint_on_an_attempt_with_no_headers_for_the_mapper_to_skip()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers = null;
        attempt.MessageMetadata["TimeSent"] = "2026-07-22T09:59:00Z";
        var stored = new Dictionary<string, object> { ["Name"] = "Stored" };
        attempt.MessageMetadata["ReceivingEndpoint"] = stored;

        FailedMessageStream.RestoreMetadataTypes(document);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attempt.MessageMetadata["ReceivingEndpoint"], Is.SameAs(stored));
            Assert.That(attempt.MessageMetadata["TimeSent"], Is.EqualTo(new DateTime(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc)), "TimeSent does not come from the headers, so it is still restored");
        }
    }

    [Test]
    public void RestoreMetadataTypes_lets_an_unexpected_fault_through()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers = new HeadersThatCannotBeRead();
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        Assert.Throws<InvalidOperationException>(() => FailedMessageStream.RestoreMetadataTypes(document), "a fault the data cannot explain is a defect that must halt the category, not be swallowed as one bad document");
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task Reads_a_document_with_a_null_attempt_and_resolves_StatusChangedAt_from_the_others(int nullAttempt)
    {
        var failure = new IngestedFailure();
        var document = failure.ToFailedMessage(numberOfAttempts: 2);
        var expected = document.ProcessingAttempts[1 - nullAttempt].AttemptedAt;
        document.ProcessingAttempts[nullAttempt] = null;
        await SeedFailedMessage(document);
        await CompleteDatabaseOperation();

        var row = (await ReadAll()).Single();

        Assert.That(row.Metadata[MigrationMetadataKeys.StatusChangedAt], Is.EqualTo(expected), "a null attempt is a data fault for the mapper to skip, not a reason to halt the category");
    }

    [Test]
    public void RestoreMetadataTypes_ignores_a_TimeSent_that_is_not_a_date_on_an_attempt_the_mapper_never_reads()
    {
        var document = new IngestedFailure().ToFailedMessage(numberOfAttempts: 2);
        document.ProcessingAttempts[0].MessageMetadata["TimeSent"] = "garbage";

        Assert.DoesNotThrow(() => FailedMessageStream.RestoreMetadataTypes(document), "a fault in data that is never copied must not halt the category");
    }

    [Test]
    public void RestoreMetadataTypes_leaves_a_document_with_no_attempts_for_the_mapper_to_skip()
    {
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts.Clear();

        Assert.DoesNotThrow(() => FailedMessageStream.RestoreMetadataTypes(document));
    }

    [Test]
    public void RestoreMetadataTypes_keeps_the_receiving_host_id_header_on_the_named_header_path()
    {
        var hostId = Guid.NewGuid();
        var document = SenderAndReceiverHeadersFailure();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers[NServiceBus.Headers.HostId] = hostId.ToString();
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(attempt.MessageMetadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(hostId));
    }

    [Test]
    public void RestoreMetadataTypes_keeps_the_receiving_host_id_header_on_the_failed_queue_path()
    {
        var hostId = Guid.NewGuid();
        var document = new IngestedFailure { QueueAddress = "Sales@MACHINE" }.ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers.Remove(NServiceBus.Headers.ProcessingEndpoint);
        attempt.Headers[NServiceBus.Headers.HostId] = hostId.ToString();
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(attempt.MessageMetadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.HostId)).EqualTo(hostId), "a host id the headers carry is not replaced by one derived from the name and host");
    }

    [Test]
    public void RestoreMetadataTypes_prefers_the_host_display_name_to_the_processing_machine()
    {
        var document = SenderAndReceiverHeadersFailure();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers[NServiceBus.Headers.HostDisplayName] = "DISPLAY";
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(attempt.MessageMetadata["ReceivingEndpoint"], Is.TypeOf<EndpointDetails>().And.Property(nameof(EndpointDetails.Host)).EqualTo("DISPLAY"));
    }

    [Test]
    public void RestoreMetadataTypes_does_not_throw_when_every_header_it_reads_is_null()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();

        foreach (var header in new[]
                 {
                     NServiceBus.Headers.OriginatingEndpoint, "NServiceBus.OriginatingMachine", NServiceBus.Headers.OriginatingHostId, NServiceBus.Headers.OriginatingAddress,
                     NServiceBus.Headers.ProcessingEndpoint, NServiceBus.Headers.HostDisplayName, "NServiceBus.ProcessingMachine", NServiceBus.Headers.HostId, "NServiceBus.FailedQ"
                 })
        {
            attempt.Headers[header] = null;
        }

        attempt.MessageMetadata["SendingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        Assert.DoesNotThrow(() => FailedMessageStream.RestoreMetadataTypes(document), "with the catch gone, a throw on any header value would halt the whole category");
    }

    [Test]
    public void RestoreMetadataTypes_restores_no_sending_endpoint_when_its_address_header_is_null()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers[NServiceBus.Headers.OriginatingAddress] = null;
        attempt.MessageMetadata["SendingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(attempt.MessageMetadata["SendingEndpoint"], Is.Null, "ingestion stores no endpoint when the address it falls back to has no value");
    }

    [Test]
    public void RestoreMetadataTypes_restores_no_receiving_endpoint_when_its_failed_queue_header_is_null()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var attempt = document.ProcessingAttempts.Single();
        attempt.Headers["NServiceBus.FailedQ"] = null;
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        FailedMessageStream.RestoreMetadataTypes(document);

        Assert.That(attempt.MessageMetadata["ReceivingEndpoint"], Is.Null, "ingestion stores no endpoint when the failed queue it falls back to has no value");
    }

    static FailedMessage SenderAndReceiverHeadersFailure()
    {
        var document = new IngestedFailure().ToFailedMessage();
        var headers = document.ProcessingAttempts.Single().Headers;
        headers[NServiceBus.Headers.OriginatingEndpoint] = "Ordering";
        headers["NServiceBus.OriginatingMachine"] = "SENDER";
        headers["NServiceBus.ProcessingMachine"] = "MACHINE";
        return document;
    }

    // The index cannot turn this TimeSent into a date, so RavenDB fails to index the document.
    static FailedMessage FailedMessageWithATimeSentThatIsNotADate()
    {
        var document = new IngestedFailure().ToFailedMessage(FailedMessageStatus.Unresolved);
        document.ProcessingAttempts.Single().MessageMetadata["TimeSent"] = "garbage";
        return document;
    }

    async Task UseCorax()
    {
        await DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation(IndexName), TestContext.CurrentContext.CancellationToken);
        await new FailedMessageViewIndex { Configuration = { ["Indexing.Static.SearchEngineType"] = SearchEngineType.Corax.ToString() } }.ExecuteAsync(DocumentStore, token: TestContext.CurrentContext.CancellationToken);

        var engine = (await DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(IndexName), TestContext.CurrentContext.CancellationToken)).SearchEngineType;
        Assert.That(engine, Is.EqualTo(SearchEngineType.Corax), "the test proves nothing about Corax if the index still runs on Lucene");
    }

    async Task<IndexState> IndexStateNow() =>
        (await DocumentStore.Maintenance.SendAsync(new GetStatisticsOperation(), TestContext.CurrentContext.CancellationToken)).Indexes.Single(index => index.Name == IndexName).State;

    async Task<List<MigrationRow>> ReadAll()
    {
        await using var source = await OpenMigrationSource();

        return (await CollectBatches(source, Category)).SelectMany(batch => batch.Rows).ToList();
    }

    // RavenDB streams in id order, so statuses are handed out by sorted id rather than by seeding order.
    async Task<List<string>> SeedInIdOrder(params FailedMessageStatus[] statuses)
    {
        var messages = statuses.Select(_ => new IngestedFailure().ToFailedMessage())
            .OrderBy(message => FailedMessageIdGenerator.MakeDocumentId(message.UniqueMessageId), StringComparer.Ordinal)
            .ToList();
        var ids = new List<string>(messages.Count);

        for (var position = 0; position < messages.Count; position++)
        {
            messages[position].Status = statuses[position];
            ids.Add((await SeedFailedMessage(messages[position])).Id);
        }

        await CompleteDatabaseOperation();

        return ids;
    }

    // Takes the walk limit as a parameter so a test can reach it without seeding the 10,000 documents of RavenDocumentStream.MaxDocumentsWalkedPerBatch.
    async Task<List<MigrationBatch>> StreamWithWalkLimit(int batchSize, int maxDocumentsWalkedPerBatch)
    {
        var settings = (RavenPersisterSettings)PersistenceSettings;
        await using var lifecycle = new RavenReadOnlySourceLifecycle(settings, new SettingsRootNamespace("ServiceControl"));
        await lifecycle.Open(TestContext.CurrentContext.CancellationToken);

        var batches = new List<MigrationBatch>();

        await foreach (var batch in RavenDocumentStream.ByPrefix(
            lifecycle, Category.Id, settings.DatabaseName, $"{FailedMessageIdGenerator.CollectionName}/", null, batchSize, maxDocumentsWalkedPerBatch,
            new FailedMessageStream(lifecycle).Project(MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses), TestContext.CurrentContext.CancellationToken))
        {
            batches.Add(batch);
        }

        return batches;
    }

    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);
    static readonly string IndexName = new FailedMessageViewIndex().IndexName;

    // The parsers read headers through IReadOnlyDictionary, so re-implementing its lookup makes every header read fail.
    sealed class HeadersThatCannotBeRead : Dictionary<string, string>, IReadOnlyDictionary<string, string>
    {
        bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value) =>
            throw new InvalidOperationException("The headers cannot be read.");
    }
}
