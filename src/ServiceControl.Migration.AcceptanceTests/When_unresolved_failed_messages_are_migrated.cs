namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;

[TestFixture]
[NonParallelizable]
class When_unresolved_failed_messages_are_migrated : MigrationAcceptanceTest
{
    [Test]
    public async Task Fills_every_column_the_newest_attempt_supplies()
    {
        var failure = new IngestedFailure { FailingEndpointAddress = "Sales@MACHINE" };
        await SeedFailure(failure, numberOfAttempts: 5);

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);
        var groups = await QueryTarget(db => db.FailedMessageGroups
            .AsNoTracking()
            .Where(group => group.FailedMessageUniqueId == failure.UniqueMessageId)
            .ToListAsync());
        var expected = failure.Groups.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(5), "the count survives even though the attempts do not");
            Assert.That(row.LastAttemptedAt, Is.EqualTo(failure.AttemptedAt));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(failure.AttemptedAt.AddMinutes(-4)));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(failure.TimeOfFailure));
            Assert.That(groups, Has.Count.EqualTo(1), "a message copied without its group rows is missing from every group view");
            Assert.That(groups.Single().GroupId, Is.EqualTo(expected.Id));
            Assert.That(groups.Single().Title, Is.EqualTo(expected.Title));
            Assert.That(groups.Single().Type, Is.EqualTo(expected.Type));
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
    public async Task A_retry_issued_message_resent_from_pending_retries_after_the_copy_is_staged()
    {
        var retryIssued = new IngestedFailure();
        await SeedFailure(retryIssued, FailedMessageStatus.RetryIssued);
        await SeedSourceRetryClaim(retryIssued.UniqueMessageIdString);

        var copy = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Copied, Is.EqualTo(1), "the test proves nothing unless the message reached the target");
            Assert.That(await QueryTarget(db => db.FailedMessageRetries.CountAsync(claim => claim.UniqueMessageId == retryIssued.UniqueMessageId)), Is.EqualTo(1),
                "the test proves nothing about releasing a migrated claim unless the copy wrote one");
        }

        // Pending Retries releases the claim, then sends a retry by id.
        await FailedMessageRetryDataStore.RemoveFailedMessageRetry(retryIssued.UniqueMessageIdString);
        string[] ids = [retryIssued.UniqueMessageIdString];
        var batchId = await RetryBatchStore.CreateBatch("migration-test-session", "retry-pending", RetryType.MultipleMessages, ids, null, DateTime.UtcNow);
        await RetryBatchStore.AssignMessagesToBatch(batchId, ids);

        var staged = await RetryStagingStore.GetMessagesToStage(batchId);

        Assert.That(staged.Select(message => message.UniqueMessageId), Is.EqualTo(ids),
            "a migrated claim that the release by message id cannot remove leaves the message unretryable for good");
    }

    [Test]
    public async Task The_required_copy_completes_the_category_before_the_host_opens()
    {
        var failures = Enumerable.Range(0, 3).Select(_ => new IngestedFailure()).ToArray();

        foreach (var failure in failures)
        {
            await SeedFailure(failure);
        }

        await MigrateThenRunHost();

        var checkpoint = await ReadCheckpoint("UnresolvedAndRetryIssuedFailedMessages");
        var response = await HttpClient.GetAsync($"api/errors/last/{failures[0].UniqueMessageIdString}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.SourceTotal, Is.EqualTo(3), "the guided run saves the source count it took before copying");
            Assert.That(response.IsSuccessStatusCode, Is.True, "the bookmark a customer had on RavenDB stopped resolving");
        }

        var fromApi = await response.Content.ReadFromJsonAsync<FailedMessageView>(SerializerOptions.Default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fromApi.Id, Is.EqualTo(failures[0].UniqueMessageIdString));
            Assert.That(fromApi.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(fromApi.MessageType, Is.EqualTo(failures[0].MessageType));
            Assert.That(fromApi.ReceivingEndpoint.Name, Is.EqualTo(failures[0].ReceivingEndpoint.Name));
            Assert.That(fromApi.QueueAddress, Is.EqualTo(failures[0].QueueAddress));
            Assert.That(fromApi.NumberOfProcessingAttempts, Is.EqualTo(1));
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

        var refused = Assert.ThrowsAsync<Exception>(async () => await MigrateCommand.Run(Settings, null, cancellation.Token));

        var checkpoint = await ReadCheckpoint("UnresolvedAndRetryIssuedFailedMessages");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Message, Does.Contain("UnresolvedAndRetryIssuedFailedMessages").And.Contain("--retry=UnresolvedAndRetryIssuedFailedMessages").And.Contain("--abandon=UnresolvedAndRetryIssuedFailedMessages"), "a fault skip leaves a required category Failed, so the run stops and names both arguments");
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

    // Seeded with its body, because the copy leaves out a message whose body the source does not hold.
    Task SeedFailure(IngestedFailure failure, FailedMessageStatus status = FailedMessageStatus.Unresolved, int numberOfAttempts = 1) =>
        SeedSourceWithBody(failure.ToFailedMessage(status, numberOfAttempts), failure.Body, failure.ContentType);
}
