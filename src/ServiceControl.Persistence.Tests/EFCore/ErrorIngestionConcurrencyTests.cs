namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Entities;

class ErrorIngestionConcurrencyTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Concurrent_writers_on_the_same_messages_neither_collide_nor_lose_attempts()
    {
        const int writers = 4;
        const int messages = 25;

        var seeds = Enumerable.Range(0, messages).Select(_ => new IngestedFailure()).ToArray();
        var baseTime = seeds[0].AttemptedAt;

        await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            await using var unitOfWork = await UnitOfWorkFactory.StartNew();

            foreach (var seed in seeds)
            {
                var attempt = seed.NextAttempt(baseTime.AddMinutes(writer));
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(attempt.Context, attempt.ProcessingAttempt, attempt.Groups);
            }

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        })));

        foreach (var seed in seeds)
        {
            var row = await GetFailedMessage(seed.UniqueMessageId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(writers), "every writer's distinct attempt must be counted exactly once");
                Assert.That(row.LastAttemptedAt, Is.EqualTo(baseTime.AddMinutes(writers - 1)), "the newest attempt must win regardless of commit order");
            }
        }
    }

    [Test]
    public async Task Concurrent_writers_leave_only_the_newest_attempts_groups()
    {
        const int writers = 4;
        const int messages = 10;

        var seeds = Enumerable.Range(0, messages).Select(_ => new IngestedFailure()).ToArray();
        var baseTime = seeds[0].AttemptedAt;

        var groupsPerWriter = Enumerable.Range(0, writers)
            .Select(writer => new List<FailedMessage.FailureGroup>
            {
                new() { Id = Guid.NewGuid().ToString(), Title = $"Writer {writer}", Type = "Exception Type and Stack Trace" }
            })
            .ToArray();

        await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            await using var unitOfWork = await UnitOfWorkFactory.StartNew();

            foreach (var seed in seeds)
            {
                var attempt = seed.NextAttempt(baseTime.AddMinutes(writer), groupsPerWriter[writer]);
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(attempt.Context, attempt.ProcessingAttempt, attempt.Groups);
            }

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        })));

        var newestGroupId = groupsPerWriter[writers - 1][0].Id;

        foreach (var seed in seeds)
        {
            var groups = await GetGroups(seed.UniqueMessageId);

            Assert.That(groups.Select(group => group.GroupId), Is.EqualTo(new[] { newestGroupId }),
                "only the newest attempt's groups may survive, regardless of commit order");
        }
    }

    // A retry acknowledgement and a later failure of the same message reach storage in whatever
    // order their batches commit, and with several ingestion hosts they need not even be on the
    // same one. Both orders are forced here rather than raced, so the assertion is about the end
    // state and not about which batch got there first.
    [Test]
    public async Task A_confirmation_arriving_after_a_later_attempt_leaves_the_message_unresolved()
    {
        var failure = new IngestedFailure();
        var retrySucceededAt = failure.AttemptedAt.AddMinutes(1);

        await Ingest(failure);
        await Ingest(failure.NextAttempt(retrySucceededAt.AddMinutes(1)));
        await ConfirmRetryAt(retrySucceededAt, failure.UniqueMessageIdString);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Unresolved), "the message failed again after the retry succeeded");
    }

    [Test]
    public async Task A_later_attempt_arriving_after_a_confirmation_leaves_the_message_unresolved()
    {
        var failure = new IngestedFailure();
        var retrySucceededAt = failure.AttemptedAt.AddMinutes(1);

        await Ingest(failure);
        await ConfirmRetryAt(retrySucceededAt, failure.UniqueMessageIdString);
        await Ingest(failure.NextAttempt(retrySucceededAt.AddMinutes(1)));

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Unresolved), "the message failed again after the retry succeeded");
    }

    [Test]
    public async Task A_redelivered_attempt_arriving_after_a_confirmation_leaves_the_message_resolved()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await ConfirmRetryAt(failure.AttemptedAt.AddMinutes(1), failure.UniqueMessageIdString);
        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Resolved), "redelivering the attempt the retry was for is not a new failure");
    }

    [Test]
    public async Task A_confirmation_releases_the_retry_claim_even_when_it_cannot_resolve()
    {
        var failure = new IngestedFailure();
        var retrySucceededAt = failure.AttemptedAt.AddMinutes(1);

        await Ingest(failure);
        await Store(new FailedMessageRetryEntity { UniqueMessageId = failure.UniqueMessageId, RetryBatchId = Guid.NewGuid() });
        await Ingest(failure.NextAttempt(retrySucceededAt.AddMinutes(1)));

        await ConfirmRetryAt(retrySucceededAt, failure.UniqueMessageIdString);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(await CountRetryRows(failure.UniqueMessageId), Is.Zero, "the retry itself completed, so its claim is released either way");
        }
    }

    [Test]
    public async Task Concurrent_writers_recording_the_same_endpoint_insert_it_once()
    {
        const int writers = 8;

        var endpoint = new EndpointDetails { Name = $"Endpoint-{Guid.NewGuid():N}", HostId = Guid.NewGuid(), Host = "Host1" };

        await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            await using var unitOfWork = await UnitOfWorkFactory.StartNew();

            await unitOfWork.Monitoring.RecordKnownEndpoint(new KnownEndpoint
            {
                EndpointDetails = endpoint,
                HostDisplayName = endpoint.Host,
                Monitored = false
            });

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        })));

        Assert.That(await GetKnownEndpoints([endpoint.GetDeterministicId()]), Has.Count.EqualTo(1));
    }
}
