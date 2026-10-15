namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Raven.Client.Documents.Operations;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Tests;

[TestFixture]
[NonParallelizable]
class When_unresolved_failed_messages_are_migrated : MigrationAcceptanceTest
{
    [Test]
    public async Task Fills_every_column_the_newest_attempt_supplies()
    {
        var failure = new IngestedFailure { FailingEndpointAddress = "Sales@MACHINE" };
        await SeedFailure(failure);

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(row.MessageType, Is.EqualTo(failure.MessageType));
            Assert.That(row.TimeSent, Is.EqualTo(failure.TimeSent));
            Assert.That(row.ConversationId, Is.EqualTo(failure.ConversationId));
            Assert.That(row.IsSystemMessage, Is.False);
            Assert.That(row.SendingEndpointName, Is.EqualTo(failure.SendingEndpoint.Name));
            Assert.That(row.SendingEndpointHostId, Is.EqualTo(failure.SendingEndpoint.HostId));
            Assert.That(row.SendingEndpointHost, Is.EqualTo(failure.SendingEndpoint.Host));
            Assert.That(row.ReceivingEndpointName, Is.EqualTo(failure.ReceivingEndpoint.Name));
            Assert.That(row.ReceivingEndpointHostId, Is.EqualTo(failure.ReceivingEndpoint.HostId));
            Assert.That(row.ReceivingEndpointHost, Is.EqualTo(failure.ReceivingEndpoint.Host));
            Assert.That(row.ExceptionType, Is.EqualTo(failure.ExceptionType));
            Assert.That(row.ExceptionMessage, Is.EqualTo(failure.ExceptionMessage));
            Assert.That(row.FailingEndpointAddress, Is.EqualTo("Sales@MACHINE"));
            Assert.That(row.HeadersJson, Does.Contain(failure.MessageId));
        }
    }

    [Test]
    public async Task Collapses_attempt_history_to_the_newest_attempt()
    {
        var failure = new IngestedFailure();
        await SeedFailure(failure, numberOfAttempts: 5);

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(5), "the count survives even though the attempts do not");
            Assert.That(row.LastAttemptedAt, Is.EqualTo(failure.AttemptedAt));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(failure.AttemptedAt.AddMinutes(-4)));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(failure.TimeOfFailure));
        }
    }

    [Test]
    public async Task A_resumed_copy_leaves_the_rows_the_first_run_wrote()
    {
        var failure = new IngestedFailure();
        await SeedFailure(failure);

        var first = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        await QueryTarget(async db =>
        {
            var existing = await db.FailedMessages.SingleAsync(row => row.UniqueMessageId == failure.UniqueMessageId);
            existing.ExceptionMessage = "Edited after the first copy, the way a live instance would have";
            await db.SaveChangesAsync();
            return existing;
        });

        await DeleteCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var second = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Copied, Is.EqualTo(1));
            Assert.That(row.ExceptionMessage, Is.EqualTo("Edited after the first copy, the way a live instance would have"), "the copier overwrote a row that was already in the target");
            Assert.That(second.Copied, Is.Zero);
            Assert.That(second.AlreadyPresent, Is.EqualTo(1), "a row the target already holds is a third outcome");
            Assert.That(second.Skipped, Is.Zero, "counting it as a skip would push a healthy category toward the halt threshold");
            Assert.That(second.SkippedIds, Is.Empty);
        }
    }

    [Test]
    public async Task Counts_only_the_statuses_this_category_owns()
    {
        var unresolved = new IngestedFailure();
        var retryIssued = new IngestedFailure();
        await SeedFailure(unresolved);
        await SeedFailure(retryIssued, FailedMessageStatus.RetryIssued);

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        // Written straight into the target, because this build has no category that copies archived messages.
        await QueryTarget(async db =>
        {
            db.FailedMessages.Add(new FailedMessageEntity
            {
                UniqueMessageId = Guid.NewGuid(),
                Status = FailedMessageStatus.Archived,
                HeadersJson = "{}",
                FailingEndpointAddress = "error"
            });

            return await db.SaveChangesAsync();
        });

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Target.Count(category), Is.EqualTo(2), "the category's count takes in the archive's rows unless it honours the category's statuses");
            Assert.That(await QueryTarget(db => db.FailedMessages.CountAsync()), Is.EqualTo(3), "the filter is doing the work, not an empty table");
        }
    }

    [Test]
    public async Task A_copied_message_keeps_its_failure_groups()
    {
        var failure = new IngestedFailure();
        await SeedFailure(failure);

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var groups = await QueryTarget(db => db.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.FailedMessageUniqueId == failure.UniqueMessageId)
            .ToListAsync());
        var expected = failure.Groups.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(groups, Has.Count.EqualTo(1), "a message copied without its group rows is missing from every group view");
            Assert.That(groups.Single().GroupId, Is.EqualTo(expected.Id));
            Assert.That(groups.Single().Title, Is.EqualTo(expected.Title));
            Assert.That(groups.Single().Type, Is.EqualTo(expected.Type));
        }
    }

    [Test]
    public async Task A_message_already_in_the_target_gets_no_group_rows_from_the_copy()
    {
        var failure = new IngestedFailure();
        await SeedFailure(failure);

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        await QueryTarget(db => db.FailedMessageGroups.Where(group => group.FailedMessageUniqueId == failure.UniqueMessageId).ExecuteDeleteAsync());
        await DeleteCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var second = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var groups = await QueryTarget(db => db.FailedMessageGroups.AsNoTracking().CountAsync(group => group.FailedMessageUniqueId == failure.UniqueMessageId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.AlreadyPresent, Is.EqualTo(1));
            Assert.That(groups, Is.Zero, "the copy wrote RavenDB's groups onto a message it did not insert");
        }
    }

    [Test]
    public async Task Answers_for_a_copied_message_on_the_URL_it_had_on_RavenDB()
    {
        var failure = new IngestedFailure();
        await SeedFailure(failure);

        await RunHostUntilTheApiAnswers();

        var response = await HttpClient.GetAsync($"api/errors/last/{failure.UniqueMessageIdString}");

        Assert.That(response.IsSuccessStatusCode, Is.True, "the bookmark a customer had on RavenDB stopped resolving");

        var fromApi = await response.Content.ReadFromJsonAsync<FailedMessageView>(SerializerOptions.Default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fromApi.Id, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(fromApi.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(fromApi.MessageType, Is.EqualTo(failure.MessageType));
            Assert.That(fromApi.ReceivingEndpoint.Name, Is.EqualTo(failure.ReceivingEndpoint.Name));
            Assert.That(fromApi.QueueAddress, Is.EqualTo(failure.QueueAddress));
            Assert.That(fromApi.NumberOfProcessingAttempts, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task The_required_copy_completes_the_category_before_the_host_opens()
    {
        var failures = Enumerable.Range(0, 3).Select(_ => new IngestedFailure()).ToArray();

        foreach (var failure in failures)
        {
            await SeedFailure(failure);
        }

        await RunHostUntilTheApiAnswers();

        var checkpoint = await ReadCheckpoint("UnresolvedAndRetryIssuedFailedMessages");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.SourceTotal, Is.Null, "the engine no longer counts the source before copying");
        }
    }

    [Test]
    public async Task Records_each_skip_under_the_reason_that_caused_it()
    {
        var good = new IngestedFailure();
        await SeedFailure(good);

        var unparseable = new IngestedFailure();
        var unparseableDocument = unparseable.ToFailedMessage();
        unparseableDocument.UniqueMessageId = "not-a-guid";
        await SeedSourceWithBody(unparseableDocument, unparseable.Body, unparseable.ContentType);

        var attemptless = new IngestedFailure();
        var attemptlessDocument = attemptless.ToFailedMessage();
        attemptlessDocument.ProcessingAttempts.Clear();
        await SeedSourceWithBody(attemptlessDocument, attemptless.Body, attemptless.ContentType);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var refused = Assert.ThrowsAsync<Exception>(async () => await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(), cancellation.Token));

        var checkpoint = await ReadCheckpoint("UnresolvedAndRetryIssuedFailedMessages");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Message, Does.Contain("UnresolvedAndRetryIssuedFailedMessages").And.Contain("--migration-retry").And.Contain("--migration-abandon"), "a fault skip leaves a required category Failed, so the host stays closed and names both commands");
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(1));
            Assert.That(checkpoint.SkippedCount, Is.EqualTo(2));
            Assert.That(checkpoint.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long>
            {
                [MigrationSkipReason.UnparseableUniqueMessageId] = 1,
                [MigrationSkipReason.NoProcessingAttempts] = 1
            }));
        }
    }

    [Test]
    public async Task A_document_whose_TimeSent_cannot_be_read_is_skipped_as_malformed_and_one_whose_host_id_is_not_a_GUID_copies_with_the_empty_host_id()
    {
        var good = new IngestedFailure();
        await SeedFailure(good);

        var badTimeSent = new IngestedFailure();
        var badTimeSentDocument = badTimeSent.ToFailedMessage();
        badTimeSentDocument.ProcessingAttempts[^1].MessageMetadata["TimeSent"] = "garbage";

        var badHostId = new IngestedFailure();
        var badHostIdDocument = badHostId.ToFailedMessage();
        var attempt = badHostIdDocument.ProcessingAttempts[^1];
        attempt.Headers[NServiceBus.Headers.HostId] = "not-a-guid";
        attempt.Headers["NServiceBus.ProcessingMachine"] = "MACHINE";
        attempt.MessageMetadata["ReceivingEndpoint"] = new Dictionary<string, object> { ["Name"] = "Stored" };

        await SeedSourceWithBodies(
            (badTimeSentDocument, badTimeSent.Body, badTimeSent.ContentType),
            (badHostIdDocument, badHostId.Body, badHostId.ContentType));

        var result = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "the copy reaches the end of the source instead of halting at the first bad document");
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { $"FailedMessages/{badTimeSent.UniqueMessageIdString}" }));
            Assert.That(result.Saved.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.MalformedValue] = 1 }));
            Assert.That(await QueryTarget(db => db.FailedMessages.Select(row => row.UniqueMessageId).ToListAsync()), Is.EquivalentTo(new[] { good.UniqueMessageId, badHostId.UniqueMessageId }));
            var copied = await QueryTarget(db => db.FailedMessages.Where(row => row.UniqueMessageId == badHostId.UniqueMessageId).SingleAsync());
            Assert.That(copied.ReceivingEndpointHostId, Is.EqualTo(Guid.Empty), "ingestion stores an unparseable host id as the empty GUID, so the message copies with it");
            Assert.That(copied.ReceivingEndpointName, Is.EqualTo("Sales"), "the endpoint is rebuilt from the headers, not taken from the stored dictionary");
            Assert.That(copied.ReceivingEndpointHost, Is.EqualTo("MACHINE"));
        }
    }

    [Test]
    public async Task A_failure_stored_without_its_time_is_skipped_when_newest_and_left_out_of_the_failure_times_when_older()
    {
        var newestHasNoTime = new IngestedFailure();
        var olderHasNoTime = new IngestedFailure();
        await SeedFailure(newestHasNoTime);
        await SeedFailure(olderHasNoTime, numberOfAttempts: 2);
        await DeleteStoredTimeOfFailure(newestHasNoTime, attempt: 0);
        await DeleteStoredTimeOfFailure(olderHasNoTime, attempt: 0);

        var result = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var copied = await GetFailedMessage(olderHasNoTime.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { $"FailedMessages/{newestHasNoTime.UniqueMessageIdString}" }), "a missing time read back as the copy's own clock, so the message copied with a made-up failure time");
            Assert.That(result.Saved.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 1 }));
            Assert.That(copied.FirstTimeOfFailure, Is.EqualTo(olderHasNoTime.TimeOfFailure), "the older attempt has no time, so only the newest one counts");
            Assert.That(copied.LastTimeOfFailure, Is.EqualTo(olderHasNoTime.TimeOfFailure), "the copy's own clock would become the last failure time");
        }
    }

    // Removes the key itself, because a stored default date is a different case that the source already reads correctly.
    Task DeleteStoredTimeOfFailure(IngestedFailure failure, int attempt) =>
        SourceStore.Operations.ForDatabase(Source.PrimaryDatabase).SendAsync(new PatchOperation(
            $"FailedMessages/{failure.UniqueMessageIdString}",
            null,
            new PatchRequest { Script = $"delete this.ProcessingAttempts[{attempt}].FailureDetails.TimeOfFailure;" }));

    // Seeded with its body, because the copy leaves out a message whose body the source does not hold.
    Task SeedFailure(IngestedFailure failure, FailedMessageStatus status = FailedMessageStatus.Unresolved, int numberOfAttempts = 1) =>
        SeedSourceWithBody(failure.ToFailedMessage(status, numberOfAttempts), failure.Body, failure.ContentType);
}
