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
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class FailedMessagesMigrationTargetTests : PersistenceTestBase
{
    readonly TargetWarnings targetWarnings = new();

    public FailedMessagesMigrationTargetTests() => RegisterServices = services => services.AddSingleton<ILoggerProvider>(targetWarnings);

    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    IMigrationCheckpointStore CheckpointStore => ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();

    IBodyStoragePersistence BodyStore => ServiceProvider.GetRequiredService<IBodyStoragePersistence>();

    static readonly MigrationBody SmallTextBody = new(Encoding.UTF8.GetBytes("<order>1</order>"), "text/xml");

    static readonly MigrationCategory UnresolvedCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.UnresolvedFailedMessages);

    static readonly MigrationCategory FailedMessagesCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.RecentAndRetriedFailedMessages);

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
    public async Task A_copied_retry_issued_message_gets_a_retry_claim_and_an_unresolved_one_does_not()
    {
        var unresolved = new IngestedFailure();
        var retryIssued = new IngestedFailure();

        var result = await Target.Write(FailedMessagesCategory, BatchOf(unresolved.ToFailedMessage(), retryIssued.ToFailedMessage(FailedMessageStatus.RetryIssued)), Checkpoint());

        var claims = await Query(db => db.FailedMessageRetries.AsNoTracking().Select(claim => new { claim.UniqueMessageId, claim.RetryBatchId, claim.StageAttempts }).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2), "the test proves nothing unless both messages reached the target");
            Assert.That(claims, Is.EqualTo(new[] { new { retryIssued.UniqueMessageId, RetryBatchId = FailedMessagesWriterBase.MigratedRetryClaimBatchId, StageAttempts = 0 } }),
                "without a claim a retry by id sends a retry-issued message again, and with one an unresolved message could not be retried by id");
        }
    }

    [Test]
    public async Task A_retry_issued_message_already_in_the_target_gets_no_retry_claim()
    {
        var present = new IngestedFailure();
        var absent = new IngestedFailure();
        await Query(db =>
        {
            db.FailedMessages.Add(new FailedMessageEntity { UniqueMessageId = present.UniqueMessageId, Status = FailedMessageStatus.RetryIssued, HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        var result = await Target.Write(FailedMessagesCategory, BatchOf(present.ToFailedMessage(FailedMessageStatus.RetryIssued), absent.ToFailedMessage(FailedMessageStatus.RetryIssued)), Checkpoint());

        var claimed = await Query(db => db.FailedMessageRetries.AsNoTracking().Select(claim => claim.UniqueMessageId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(claimed, Is.EqualTo(new[] { absent.UniqueMessageId }), "a message the insert turned away keeps what the target had, so it gets no claim from the copy");
        }
    }

    [Test]
    public async Task Two_documents_sharing_a_unique_message_id_get_a_retry_claim_only_when_the_kept_one_is_retry_issued()
    {
        var unresolvedKept = new IngestedFailure();
        var retryIssuedKept = new IngestedFailure();
        var retryIssuedTurnedAway = new IngestedFailure().ToFailedMessage(FailedMessageStatus.RetryIssued);
        retryIssuedTurnedAway.UniqueMessageId = unresolvedKept.UniqueMessageIdString;
        var unresolvedTurnedAway = new IngestedFailure().ToFailedMessage();
        unresolvedTurnedAway.UniqueMessageId = retryIssuedKept.UniqueMessageIdString;

        var result = await Target.Write(FailedMessagesCategory, BatchOf(
            unresolvedKept.ToFailedMessage(), retryIssuedTurnedAway,
            retryIssuedKept.ToFailedMessage(FailedMessageStatus.RetryIssued), unresolvedTurnedAway), Checkpoint());

        var claimed = await Query(db => db.FailedMessageRetries.AsNoTracking().Select(claim => claim.UniqueMessageId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(result.AlreadyPresent, Is.EqualTo(2));
            Assert.That(claimed, Is.EqualTo(new[] { retryIssuedKept.UniqueMessageId }), "the claim follows the status of the row the insert kept, not of the document it turned away");
        }
    }

    [Test]
    public async Task Copied_counts_the_messages_inserted_not_the_messages_handed_in()
    {
        var present = new IngestedFailure();
        var absent = new IngestedFailure();
        await Target.Write(FailedMessagesCategory, BatchOf(present.ToFailedMessage()), Checkpoint());
        var saved = await CheckpointStore.Read(MigrationCategoryIds.RecentAndRetriedFailedMessages);

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
    public async Task A_change_is_not_logged_when_the_batch_does_not_commit()
    {
        var longType = new IngestedFailure { MessageType = new string('m', 600) };
        await Target.Write(FailedMessagesCategory, BatchOf(new IngestedFailure().ToFailedMessage()), Checkpoint());

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => Target.Write(FailedMessagesCategory, BatchOf(longType.ToFailedMessage()), Checkpoint()),
            "the checkpoint it extends is older than the stored one, so the save in the batch's transaction is refused");
        Assert.That(targetWarnings.Messages, Has.None.StartsWith("Changed "), "a change line written before the commit would name a row SQL never stored");
    }

    [Test]
    public async Task A_second_document_with_the_same_message_id_reports_no_change_of_its_own()
    {
        var first = new IngestedFailure();
        var second = new IngestedFailure { MessageType = new string('m', 600) }.ToFailedMessage();
        second.UniqueMessageId = first.UniqueMessageIdString;
        var batch = BatchOf(first.ToFailedMessage(), second);

        var changes = await PreparedSkips.ChangesFrom(ServiceProvider, Writer(), batch);

        Assert.That(changes, Is.Empty, "the insert keeps the first document, so the second's cut message type never reaches SQL");
    }

    [Test]
    public async Task A_message_type_longer_than_its_column_is_copied_with_a_change_naming_its_length()
    {
        var longType = new IngestedFailure { MessageType = new string('m', 600) };
        var batch = BatchOf(longType.ToFailedMessage());

        var changes = await PreparedSkips.ChangesFrom(ServiceProvider, Writer(), batch);
        var result = await Target.Write(FailedMessagesCategory, batch, Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new[] { ($"FailedMessages/{longType.UniqueMessageIdString}", "Cut the message type from 600 characters to its last 450, as ingestion does.") }));
            Assert.That(result.Changed, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task A_failure_group_with_no_id_or_no_title_is_reported_as_a_change_naming_the_group()
    {
        var failure = new IngestedFailure
        {
            Groups =
            [
                new() { Id = "group-kept", Title = null, Type = "Exception Type" },
                new() { Id = null!, Title = "Has no id", Type = "Message Type" },
                null!
            ]
        };
        var batch = BatchOf(failure.ToFailedMessage());

        var changes = await PreparedSkips.ChangesFrom(ServiceProvider, Writer(), batch);
        var result = await Target.Write(FailedMessagesCategory, batch, Checkpoint());
        var groups = await Query(db => db.FailedMessageGroups.AsNoTracking().Select(group => new { group.GroupId, group.Title }).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new[]
            {
                ($"FailedMessages/{failure.UniqueMessageIdString}",
                    "Dropped the failure groups with no id, because SQL keys a group by its id: the group titled 'Has no id', an empty entry. Stored an empty title or type for the failure groups that had none: 'group-kept'.")
            }));
            Assert.That(result.Changed, Is.EqualTo(1));
            Assert.That(groups.Select(group => (group.GroupId, group.Title)), Is.EqualTo(new[] { ("group-kept", "") }), "the message is missing from the group with no id, which the line has to say");
        }
    }

    [Test]
    public async Task Two_failure_groups_of_one_message_with_one_key_are_reported_as_a_change_naming_both()
    {
        var failure = new IngestedFailure
        {
            Groups =
            [
                new() { Id = "group-a", Title = "First", Type = "Exception Type" },
                new() { Id = "group-a", Title = "Second", Type = "Exception Type" }
            ]
        };
        var batch = BatchOf(failure.ToFailedMessage());

        var changes = await PreparedSkips.ChangesFrom(ServiceProvider, Writer(), batch);
        var result = await Target.Write(FailedMessagesCategory, batch, Checkpoint());
        var titles = await Query(db => db.FailedMessageGroups.AsNoTracking().Select(group => group.Title).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new[]
            {
                ($"FailedMessages/{failure.UniqueMessageIdString}", "Kept the failure group 'group-a' titled 'First' and dropped 'group-a' titled 'Second', because SQL treats their ids as one key.")
            }));
            Assert.That(result.Changed, Is.EqualTo(1));
            Assert.That(titles, Is.EqualTo(new[] { "First" }), "the line names the group the insert kept");
        }
    }

    [Test]
    public async Task A_retry_issued_message_gets_a_retry_claim_only_when_the_source_still_held_one()
    {
        var released = new IngestedFailure();
        var held = new IngestedFailure();
        var batch = BatchOf(
            WithClaimHeld(RowOf(released.ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody), false),
            WithClaimHeld(RowOf(held.ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody), true));

        var result = await Target.Write(FailedMessagesCategory, batch, Checkpoint());
        var claims = await Query(db => db.FailedMessageRetries.AsNoTracking().Select(claim => claim.UniqueMessageId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2), "the test proves nothing unless both messages reached the target");
            Assert.That(claims, Is.EqualTo(new[] { held.UniqueMessageId }), "a claim the source had released would block a retry by id that RavenDB would have sent");
            Assert.That(result.Changed, Is.Zero, "the claims match the source, so nothing changed on the way in");
        }
    }

    [Test]
    public async Task A_retry_by_id_after_the_copy_stages_the_message_whose_claim_the_source_had_released()
    {
        var released = new IngestedFailure();
        var held = new IngestedFailure();
        await Target.Write(FailedMessagesCategory, BatchOf(
            WithClaimHeld(RowOf(released.ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody), false),
            WithClaimHeld(RowOf(held.ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody), true)), Checkpoint());

        string[] ids = [released.UniqueMessageIdString, held.UniqueMessageIdString];
        var batchId = await RetryBatchStore.CreateBatch("migration-test-session", "retry-by-id", RetryType.MultipleMessages, ids, null, DateTime.UtcNow);
        await RetryBatchStore.AssignMessagesToBatch(batchId, ids);

        var staged = await RetryStagingStore.GetMessagesToStage(batchId);

        Assert.That(staged.Select(message => message.UniqueMessageId), Is.EqualTo(new[] { released.UniqueMessageIdString }),
            "RavenDB would send the released message and hold back the claimed one, so after the move a retry by id must do the same");
    }

    [Test]
    public void A_retry_issued_message_without_its_claim_flag_fails_the_write_naming_the_row()
    {
        var document = new IngestedFailure().ToFailedMessage(FailedMessageStatus.RetryIssued);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(FailedMessagesCategory, BatchOf(WithClaimHeld(RowOf(document, SmallTextBody), null)), Checkpoint()));

        Assert.That(exception.Message, Does.Contain($"'FailedMessages/{document.UniqueMessageId}'").And.Contain(MigrationMetadataKeys.RetryClaimHeld),
            "the source resolves the flag for every retry-issued message, so a missing one is a defect that must not pass as a held claim");
    }

    [Test]
    public async Task A_retry_issued_message_whose_claim_flag_is_not_a_bool_fails_the_write_and_stores_nothing()
    {
        var document = new IngestedFailure().ToFailedMessage(FailedMessageStatus.RetryIssued);
        var row = WithClaimHeld(RowOf(document, SmallTextBody), null);
        row = row with { Metadata = new Dictionary<string, object>(row.Metadata) { [MigrationMetadataKeys.RetryClaimHeld] = "true" } };

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(FailedMessagesCategory, BatchOf(row), Checkpoint()));
        var claims = await Query(db => db.FailedMessageRetries.AsNoTracking().CountAsync());
        var messages = await Query(db => db.FailedMessages.AsNoTracking().CountAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"'FailedMessages/{document.UniqueMessageId}'").And.Contain(MigrationMetadataKeys.RetryClaimHeld).And.Contain(typeof(string).FullName));
            Assert.That((claims, messages), Is.EqualTo((0, 0)), "a flag the writer cannot read must stop the batch before anything is stored, not be read as either answer");
        }
    }

    [Test]
    public async Task Two_retry_issued_documents_sharing_a_unique_message_id_take_the_claim_flag_of_the_first()
    {
        var first = new IngestedFailure();
        var second = first.ToFailedMessage(FailedMessageStatus.RetryIssued);
        var batch = BatchOf(
            WithClaimHeld(RowOf(first.ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody), false),
            WithClaimHeld(RowOf(second, SmallTextBody) with { SourceId = "FailedMessages/second" }, true));

        var result = await Target.Write(FailedMessagesCategory, batch, Checkpoint());
        var claims = await Query(db => db.FailedMessageRetries.AsNoTracking().CountAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "the test proves nothing unless the insert kept one of the two");
            Assert.That(claims, Is.Zero, "the insert keeps the first document, whose claim the source had released, so the second's held flag must not add one");
        }
    }

    [Test]
    public async Task Copying_a_batch_with_a_held_claim_again_leaves_one_claim()
    {
        var held = new IngestedFailure();
        var batch = BatchOf(held.ToFailedMessage(FailedMessageStatus.RetryIssued));

        var first = await Target.Write(FailedMessagesCategory, batch, Checkpoint());
        await Target.Write(FailedMessagesCategory, batch, first.Saved);
        var claims = await Query(db => db.FailedMessageRetries.AsNoTracking().Select(claim => claim.UniqueMessageId).ToListAsync());

        Assert.That(claims, Is.EqualTo(new[] { held.UniqueMessageId }), "a retry re-reads rows an earlier pass copied, and must not add a second claim or fail on the first");
    }

    [Test]
    public async Task A_changed_message_the_target_already_holds_is_not_reported_again()
    {
        var changed = new IngestedFailure { MessageType = new string('m', 600) };
        var batch = BatchOf(changed.ToFailedMessage());

        var first = await Target.Write(FailedMessagesCategory, batch, Checkpoint());
        var again = await Target.Write(FailedMessagesCategory, batch, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((first.Changed, again.Changed), Is.EqualTo((1, 0)), "a retry re-reads rows an earlier pass copied, and this write changed nothing");
            Assert.That(again.AlreadyPresent, Is.EqualTo(1));
            Assert.That(targetWarnings.Messages.Count(message => message.StartsWith("Changed ")), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Count_of_the_required_copy_takes_in_every_failed_message_row_whatever_its_status_or_claim()
    {
        var holder = new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived);
        await Target.Write(FailedMessagesCategory, BatchOf(
            RowOf(new IngestedFailure().ToFailedMessage(), SmallTextBody),
            RowOf(new IngestedFailure().ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody),
            WithClaimHeld(RowOf(holder, SmallTextBody), true)), Checkpoint());
        await Query(db =>
        {
            db.FailedMessages.AddRange(
                new FailedMessageEntity { UniqueMessageId = Guid.NewGuid(), Status = FailedMessageStatus.Archived, HeadersJson = "{}", FailingEndpointAddress = "error" },
                new FailedMessageEntity { UniqueMessageId = Guid.NewGuid(), Status = FailedMessageStatus.Resolved, HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Target.Count(FailedMessagesCategory), Is.EqualTo(5), "while ServiceControl is stopped the table holds only what the required copy wrote, recent errors included, so the count is the whole table");
            Assert.That(await Query(db => db.FailedMessages.CountAsync()), Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Count_of_the_unresolved_copy_takes_in_only_unresolved_messages()
    {
        await Target.Write(UnresolvedCategory, BatchOf(new IngestedFailure().ToFailedMessage(), new IngestedFailure().ToFailedMessage()), Checkpoint(MigrationCategoryIds.UnresolvedFailedMessages));
        await Query(db =>
        {
            db.FailedMessages.AddRange(
                new FailedMessageEntity { UniqueMessageId = Guid.NewGuid(), Status = FailedMessageStatus.RetryIssued, HeadersJson = "{}", FailingEndpointAddress = "error" },
                new FailedMessageEntity { UniqueMessageId = Guid.NewGuid(), Status = FailedMessageStatus.Archived, HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        Assert.That(await Target.Count(UnresolvedCategory), Is.EqualTo(2));
    }

    [Test]
    public async Task A_claim_holder_of_any_status_gets_a_retry_claim_and_a_message_without_one_does_not()
    {
        var unresolved = new IngestedFailure();
        var archived = new IngestedFailure();
        var resolved = new IngestedFailure();
        var withoutClaim = new IngestedFailure();

        var result = await Target.Write(FailedMessagesCategory, BatchOf(
            WithClaimHeld(RowOf(unresolved.ToFailedMessage(), SmallTextBody), true),
            WithClaimHeld(RowOf(archived.ToFailedMessage(FailedMessageStatus.Archived), SmallTextBody), true),
            WithClaimHeld(RowOf(resolved.ToFailedMessage(FailedMessageStatus.Resolved), SmallTextBody), true),
            WithClaimHeld(RowOf(withoutClaim.ToFailedMessage(FailedMessageStatus.Archived), SmallTextBody), false)), Checkpoint());

        var rows = await Query(db => db.FailedMessages.AsNoTracking().ToDictionaryAsync(message => message.UniqueMessageId, message => message.Status));
        var claimed = await Query(db => db.FailedMessageRetries.AsNoTracking().Select(claim => claim.UniqueMessageId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(4));
            Assert.That(rows[archived.UniqueMessageId], Is.EqualTo(FailedMessageStatus.Archived), "the row keeps the status it had in the source");
            Assert.That(rows[resolved.UniqueMessageId], Is.EqualTo(FailedMessageStatus.Resolved));
            Assert.That(claimed, Is.EquivalentTo(new[] { unresolved.UniqueMessageId, archived.UniqueMessageId, resolved.UniqueMessageId }), "a retry by id stays blocked for every holder, whatever its status");
        }
    }

    [Test]
    public async Task A_claim_holder_already_in_the_target_gets_no_retry_claim_from_the_copy()
    {
        var present = new IngestedFailure();
        await Query(db =>
        {
            db.FailedMessages.Add(new FailedMessageEntity { UniqueMessageId = present.UniqueMessageId, Status = FailedMessageStatus.Unresolved, HeadersJson = "{}", FailingEndpointAddress = "error" });
            return db.SaveChangesAsync();
        });

        var result = await Target.Write(FailedMessagesCategory, BatchOf(WithClaimHeld(RowOf(present.ToFailedMessage(), SmallTextBody), true)), Checkpoint());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(await Query(db => db.FailedMessageRetries.CountAsync()), Is.Zero);
        }
    }

    [Test]
    public async Task The_unresolved_copy_inserts_unresolved_messages_with_their_groups_and_bodies_and_no_claim()
    {
        var failure = new IngestedFailure { ContentType = "application/octet-stream" };
        var body = new MigrationBody(new byte[] { 0x08, 0x00, 0x96, 0x01 }, "application/octet-stream");

        var result = await Target.Write(UnresolvedCategory, BatchOf(RowOf(failure.ToFailedMessage(), body)), Checkpoint(MigrationCategoryIds.UnresolvedFailedMessages));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(await Query(db => db.FailedMessageGroups.CountAsync(group => group.FailedMessageUniqueId == failure.UniqueMessageId)), Is.EqualTo(failure.Groups.Count));
            Assert.That(await BodyStored(failure.UniqueMessageIdString), Is.True);
            Assert.That(await Query(db => db.FailedMessageRetries.CountAsync()), Is.Zero);
        }
    }

    [Test]
    public async Task A_row_ingestion_wrote_first_wins_over_the_unresolved_copy_with_its_attempt_count_payload_and_body()
    {
        var failure = new IngestedFailure { ContentType = "application/octet-stream" };
        var liveBody = new byte[] { 0x01, 0x02 };
        await Query(db =>
        {
            db.FailedMessages.Add(new FailedMessageEntity
            {
                UniqueMessageId = failure.UniqueMessageId,
                Status = FailedMessageStatus.Unresolved,
                ExceptionMessage = "Written by ingestion",
                NumberOfProcessingAttempts = 1,
                HeadersJson = "{}",
                FailingEndpointAddress = "error",
                BodyStoredExternally = true
            });
            return db.SaveChangesAsync();
        });
        await BodyStore.WriteBody(failure.UniqueMessageIdString, liveBody, "application/octet-stream");

        var result = await Target.Write(UnresolvedCategory, BatchOf(RowOf(failure.ToFailedMessage(numberOfAttempts: 3), new MigrationBody(new byte[] { 0x09, 0x09, 0x09 }, "application/octet-stream"))), Checkpoint(MigrationCategoryIds.UnresolvedFailedMessages));

        var row = await Query(db => db.FailedMessages.AsNoTracking().SingleAsync(message => message.UniqueMessageId == failure.UniqueMessageId));
        var stored = await BodyStore.ReadBody(failure.UniqueMessageIdString);
        using var memory = new MemoryStream();
        await stored.Stream.CopyToAsync(memory);
        await stored.Stream.DisposeAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(1), "an existing SQL row counts as already present, not as a skip");
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(row.ExceptionMessage, Is.EqualTo("Written by ingestion"), "SQL wins, so its payload stays even when the source holds a newer attempt");
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(1), "SQL wins, so its attempt count omits the earlier RavenDB attempts");
            Assert.That(memory.ToArray(), Is.EqualTo(liveBody), "the migration never replaces a body a live row already holds");
        }
    }

    [Test]
    public async Task A_body_stored_after_the_present_check_and_before_the_body_write_is_not_replaced()
    {
        // The present check and the body write are not atomic, so a body that ingestion stores between them has to survive the write.
        var failure = new IngestedFailure { ContentType = "application/octet-stream" };
        var liveBody = new byte[] { 0x01, 0x02 };
        await BodyStore.WriteBody(failure.UniqueMessageIdString, liveBody, "application/octet-stream");

        await Target.Write(UnresolvedCategory, BatchOf(RowOf(failure.ToFailedMessage(), new MigrationBody(new byte[] { 0x09, 0x09, 0x09 }, "application/octet-stream"))), Checkpoint(MigrationCategoryIds.UnresolvedFailedMessages));

        var stored = await BodyStore.ReadBody(failure.UniqueMessageIdString);
        using var memory = new MemoryStream();
        await stored.Stream.CopyToAsync(memory);
        await stored.Stream.DisposeAsync();

        Assert.That(memory.ToArray(), Is.EqualTo(liveBody));
    }

    // Known release blocker, not accepted behavior: docs/migration/failed-message-migration-split-plan.md describes the interleaving.
    // The test pins today's behavior and changes no confirmation handling, so it has to be rewritten when the blocker is resolved.
    [Test]
    public async Task Known_release_blocker_a_retry_confirmation_for_a_message_the_unresolved_copy_has_not_inserted_is_consumed_and_the_message_arrives_unresolved()
    {
        var failure = new IngestedFailure();
        var confirmation = new MessageContext(
            Guid.NewGuid().ToString(),
            new Dictionary<string, string>
            {
                ["ServiceControl.Retry.Successful"] = string.Empty,
                ["ServiceControl.Retry.UniqueMessageId"] = failure.UniqueMessageIdString
            },
            ReadOnlyMemory<byte>.Empty,
            new TransportTransaction(),
            "receiveAddress",
            new ContextBag());
        var unitOfWork = await IngestionUnitOfWorkFactory.StartNew();

        await new RetryConfirmationProcessor(new FakeDomainEvents()).Process([confirmation], unitOfWork);

        Assert.DoesNotThrowAsync(() => unitOfWork.Complete(TestContext.CurrentContext.CancellationToken), "a confirmation for a missing row succeeds today, which is how it is consumed");
        Assert.That(await Query(db => db.FailedMessages.CountAsync()), Is.Zero, "the confirmation records nothing for a row that is absent");

        await Target.Write(UnresolvedCategory, BatchOf(failure.ToFailedMessage()), Checkpoint(MigrationCategoryIds.UnresolvedFailedMessages));

        var row = await Query(db => db.FailedMessages.AsNoTracking().SingleAsync(message => message.UniqueMessageId == failure.UniqueMessageId));
        Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Unresolved), "the consumed confirmation no longer resolves the message");
    }

    [Test]
    public void The_unresolved_copy_follows_the_required_copy_checkpoint_layout()
    {
        Assert.That(UnresolvedCategory.Kind, Is.EqualTo(MigrationCategoryKind.Optional));
    }

    [Test]
    public async Task Writes_a_message_ingestion_has_not_stored_even_when_the_target_holds_a_claim_for_another()
    {
        var other = new IngestedFailure();
        var failure = new IngestedFailure();
        await Target.Write(FailedMessagesCategory, BatchOf(RowOf(other.ToFailedMessage(FailedMessageStatus.RetryIssued), SmallTextBody)), Checkpoint());

        var result = await Target.Write(UnresolvedCategory, BatchOf(failure.ToFailedMessage()), Checkpoint(MigrationCategoryIds.UnresolvedFailedMessages));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(await Query(db => db.FailedMessageRetries.CountAsync()), Is.EqualTo(1), "the unresolved copy neither adds nor removes a claim");
        }
    }

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
            Assert.That(await CheckpointStore.Read(MigrationCategoryIds.RecentAndRetriedFailedMessages), Is.Null, "a checkpoint past the batch would stop a retry from reading it again");
        }
    }

    [Test]
    public void A_document_that_cannot_be_prepared_fails_the_write_naming_the_row_and_category()
    {
        // The source rebuilds SendingEndpoint as EndpointDetails, so a value it left as stored is an engine defect the translator's cast rejects.
        var document = new IngestedFailure().ToFailedMessage();
        document.ProcessingAttempts[^1].MessageMetadata["SendingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(FailedMessagesCategory, BatchOf(document), Checkpoint()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"'FailedMessages/{document.UniqueMessageId}'").And.Contain(MigrationCategoryIds.RecentAndRetriedFailedMessages).And.Contain(nameof(InvalidCastException)),
                "the halt's LastError is built from this message, and without the row the operator cannot tell which document to look at");
            Assert.That(exception.InnerException, Is.TypeOf<InvalidCastException>());
        }
    }

    [Test]
    public void A_row_that_reaches_the_writer_without_a_body_fails_the_write_naming_the_row_and_category()
    {
        var document = new IngestedFailure().ToFailedMessage();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(FailedMessagesCategory, BatchOf(RowOf(document, body: null)), Checkpoint()));

        Assert.That(exception.Message, Does.Contain($"'FailedMessages/{document.UniqueMessageId}'").And.Contain(MigrationCategoryIds.RecentAndRetriedFailedMessages));
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

    // Every row carries the values the source resolves, as a real source's rows do, and a retry-issued one finds its claim still held.
    static MigrationRow RowOf(FailedMessage document, MigrationBody body)
    {
        var metadata = new Dictionary<string, object>
        {
            [MigrationMetadataKeys.StatusChangedAt] = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            [MigrationMetadataKeys.LastModified] = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)
        };

        if (document.Status == FailedMessageStatus.RetryIssued)
        {
            metadata[MigrationMetadataKeys.RetryClaimHeld] = true;
        }

        return new($"FailedMessages/{document.UniqueMessageId}", document, metadata, body);
    }

    // A null removes the flag, as a source that never resolved it would send the row.
    static MigrationRow WithClaimHeld(MigrationRow row, bool? held)
    {
        var metadata = new Dictionary<string, object>(row.Metadata);
        metadata.Remove(MigrationMetadataKeys.RetryClaimHeld);

        if (held is { } value)
        {
            metadata[MigrationMetadataKeys.RetryClaimHeld] = value;
        }

        return row with { Metadata = metadata };
    }

    sealed class UnwritableBodyStore : IBodyStoragePersistence
    {
        public const string Failure = "The body store is unreachable";

        public Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default) => Task.FromException(new IOException(Failure));

        public Task<MessageBodyFileResult> ReadBody(string bodyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteBodyIfExists(string bodyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    FailedMessagesWriter Writer() => new(ServiceProvider.GetRequiredService<IMigrationSqlDialect>(), BodyStore, ServiceProvider.GetRequiredService<EFPersisterSettings>());

    static MigrationCheckpoint Checkpoint(string categoryId = MigrationCategoryIds.RecentAndRetriedFailedMessages) =>
        new(categoryId, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null);
}
