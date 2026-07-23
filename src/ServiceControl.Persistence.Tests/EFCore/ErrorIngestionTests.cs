namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

class ErrorIngestionTests : ErrorIngestionTestBase
{
    [Test]
    public async Task First_failure_stores_the_message()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(1));
            Assert.That(row.LastAttemptedAt, Is.EqualTo(failure.AttemptedAt));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(failure.TimeOfFailure));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(failure.TimeOfFailure));
            Assert.That(row.MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(row.MessageType, Is.EqualTo(failure.MessageType));
            Assert.That(row.TimeSent, Is.EqualTo(failure.TimeSent));
            Assert.That(row.ConversationId, Is.EqualTo(failure.ConversationId));
            Assert.That(row.QueueAddress, Is.EqualTo(failure.QueueAddress));
            Assert.That(row.SendingEndpointName, Is.EqualTo(failure.SendingEndpoint.Name));
            Assert.That(row.SendingEndpointHostId, Is.EqualTo(failure.SendingEndpoint.HostId));
            Assert.That(row.SendingEndpointHost, Is.EqualTo(failure.SendingEndpoint.Host));
            Assert.That(row.ReceivingEndpointName, Is.EqualTo(failure.ReceivingEndpoint.Name));
            Assert.That(row.ReceivingEndpointHostId, Is.EqualTo(failure.ReceivingEndpoint.HostId));
            Assert.That(row.ReceivingEndpointHost, Is.EqualTo(failure.ReceivingEndpoint.Host));
            Assert.That(row.ExceptionType, Is.EqualTo(failure.ExceptionType));
            Assert.That(row.ExceptionMessage, Is.EqualTo(failure.ExceptionMessage));
            Assert.That(row.IsSystemMessage, Is.False);
            Assert.That(row.HeadersJson, Does.Contain(failure.MessageId));
            Assert.That(row.BodyText, Is.EqualTo(Encoding.UTF8.GetString(failure.Body)));
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(row.BodySize, Is.EqualTo(failure.Body.Length));
            Assert.That(row.BodyContentType, Is.EqualTo(failure.ContentType));
            Assert.That(row.StatusChangedAt, Is.EqualTo(row.LastModified));
        }

        var groups = await GetGroups(failure.UniqueMessageId);

        Assert.That(groups, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(groups[0].GroupId, Is.EqualTo(failure.Groups[0].Id));
            Assert.That(groups[0].Title, Is.EqualTo(failure.Groups[0].Title));
            Assert.That(groups[0].Type, Is.EqualTo(failure.Groups[0].Type));
        }
    }

    [Test]
    public async Task Later_attempt_replaces_the_stored_attempt()
    {
        var first = new IngestedFailure();
        await Ingest(first);

        var second = new IngestedFailure
        {
            MessageId = first.MessageId,
            EndpointName = first.EndpointName,
            AttemptedAt = first.AttemptedAt.AddMinutes(5),
            TimeOfFailure = first.TimeOfFailure.AddMinutes(5),
            ExceptionMessage = "A different failure",
            Body = Encoding.UTF8.GetBytes("<order>2</order>"),
            Groups = [new FailedMessage.FailureGroup { Id = Guid.NewGuid().ToString(), Title = "Another group", Type = "Exception Type and Stack Trace" }]
        };
        await Ingest(second);

        var row = await GetFailedMessage(first.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(2));
            Assert.That(row.LastAttemptedAt, Is.EqualTo(second.AttemptedAt));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(first.TimeOfFailure));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(second.TimeOfFailure));
            Assert.That(row.ExceptionMessage, Is.EqualTo(second.ExceptionMessage));
            Assert.That(row.BodyText, Is.EqualTo("<order>2</order>"));
        }

        var groups = await GetGroups(first.UniqueMessageId);

        Assert.That(groups, Has.Count.EqualTo(1), "Groups are replaced wholesale");
        Assert.That(groups[0].GroupId, Is.EqualTo(second.Groups[0].Id));
    }

    [Test]
    public async Task Refailure_of_a_resolved_message_restarts_the_retention_clock()
    {
        var failure = new IngestedFailure();
        await Ingest(failure);
        await ConfirmRetry(failure.UniqueMessageIdString);

        var resolved = await GetFailedMessage(failure.UniqueMessageId);
        Assert.That(resolved.Status, Is.EqualTo(FailedMessageStatus.Resolved));

        AdvanceClock(TimeSpan.FromMinutes(1));

        await Ingest(failure.NextAttempt(failure.AttemptedAt.AddMinutes(1)));

        var refailed = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refailed.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(refailed.StatusChangedAt, Is.GreaterThan(resolved.StatusChangedAt));
        }
    }

    [Test]
    public async Task Refailure_of_an_unresolved_message_keeps_the_retention_clock()
    {
        var failure = new IngestedFailure();
        await Ingest(failure);

        var first = await GetFailedMessage(failure.UniqueMessageId);

        AdvanceClock(TimeSpan.FromMinutes(1));

        await Ingest(failure.NextAttempt(failure.AttemptedAt.AddMinutes(1)));

        var second = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(second.StatusChangedAt, Is.EqualTo(first.StatusChangedAt));
            Assert.That(second.LastModified, Is.GreaterThan(first.LastModified));
        }
    }

    [Test]
    public async Task Redelivery_of_a_stored_attempt_is_not_a_new_attempt()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task Two_attempts_of_one_message_in_one_batch_fold_into_one_row()
    {
        var first = new IngestedFailure();
        var second = first.NextAttempt(first.AttemptedAt.AddMinutes(5));

        await Ingest(first, second);

        var row = await GetFailedMessage(first.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(2));
            Assert.That(row.LastAttemptedAt, Is.EqualTo(second.AttemptedAt));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(first.TimeOfFailure));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(second.TimeOfFailure));
        }
    }

    [Test]
    public async Task Duplicate_attempt_within_one_batch_counts_once()
    {
        var failure = new IngestedFailure();

        await Ingest(failure, failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task An_older_attempt_counts_but_does_not_overwrite_the_newer_one()
    {
        var newer = new IngestedFailure { ExceptionMessage = "The newer failure" };
        await Ingest(newer);

        var older = new IngestedFailure
        {
            MessageId = newer.MessageId,
            EndpointName = newer.EndpointName,
            AttemptedAt = newer.AttemptedAt.AddMinutes(-5),
            TimeOfFailure = newer.TimeOfFailure.AddMinutes(-5),
            ExceptionMessage = "The older failure"
        };
        await Ingest(older);

        var row = await GetFailedMessage(newer.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(2));
            Assert.That(row.LastAttemptedAt, Is.EqualTo(newer.AttemptedAt));
            Assert.That(row.ExceptionMessage, Is.EqualTo(newer.ExceptionMessage));
            Assert.That(row.FirstTimeOfFailure, Is.EqualTo(older.TimeOfFailure));
            Assert.That(row.LastTimeOfFailure, Is.EqualTo(newer.TimeOfFailure));
        }
    }

    [Test]
    public async Task A_confirmed_retry_resolves_the_message_and_drops_its_retry_row()
    {
        var failure = new IngestedFailure();
        await Ingest(failure);
        await Store(new FailedMessageRetryEntity { UniqueMessageId = failure.UniqueMessageId, RetryId = "RetryBatches/1" });

        await ConfirmRetry(failure.UniqueMessageIdString);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            Assert.That(row.StatusChangedAt, Is.EqualTo(row.LastModified));
        }

        Assert.That(await CountRetryRows(failure.UniqueMessageId), Is.Zero);
    }

    [Test]
    public async Task A_failure_confirmed_in_the_same_batch_ends_resolved()
    {
        var failure = new IngestedFailure();

        await InBatch(async unitOfWork =>
        {
            await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            await unitOfWork.Recoverability.RecordSuccessfulRetry(failure.UniqueMessageIdString);
        });

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.Status, Is.EqualTo(FailedMessageStatus.Resolved));
    }

    [Test]
    public async Task A_batch_can_hold_more_messages_than_a_statement_can_hold_parameters()
    {
        // SQL Server allows 2100 parameters per statement, which a batch this size would blow
        // through several times over if the rows were parameterized instead of bulk copied.
        var failures = Enumerable.Range(0, 250).Select(_ => new IngestedFailure()).ToArray();

        await Ingest(failures);

        foreach (var failure in failures)
        {
            var row = await GetFailedMessage(failure.UniqueMessageId);

            Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Concurrent_recording_stores_every_message()
    {
        var failures = Enumerable.Range(0, 32).Select(_ => new IngestedFailure()).ToArray();

        await InBatch(async unitOfWork =>
        {
            // Mirrors ErrorProcessor, which records the whole batch through Task.WhenAll
            await Task.WhenAll(failures.Select(failure => Task.Run(() =>
                unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups))));
        });

        var stored = new List<FailedMessageEntity>();
        foreach (var failure in failures)
        {
            stored.Add(await GetFailedMessage(failure.UniqueMessageId));
        }

        Assert.That(stored.Select(row => row.UniqueMessageId), Is.Unique);
        Assert.That(stored, Has.Count.EqualTo(failures.Length));
    }
}
