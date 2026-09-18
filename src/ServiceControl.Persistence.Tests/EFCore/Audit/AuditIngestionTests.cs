namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Infrastructure;

class AuditIngestionTests : AuditIngestionTestBase
{
    [Test]
    public async Task Writes_every_mapped_column_of_an_audit_message()
    {
        var audit = new IngestedAudit { IsSystemMessage = true };

        await IngestAudit(audit);

        var row = await GetAuditMessage(audit.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.CreatedOn, Is.EqualTo(IngestionHour));
            Assert.That(row.MessageId, Is.EqualTo(audit.MessageId));
            Assert.That(row.MessageType, Is.EqualTo(audit.MessageType));
            Assert.That(row.TimeSent, Is.EqualTo(audit.TimeSent));
            Assert.That(row.ProcessedAt, Is.EqualTo(audit.ProcessingEnded));
            Assert.That(row.ConversationId, Is.EqualTo(audit.ConversationId));
            Assert.That(row.IsSystemMessage, Is.True);
            Assert.That(row.Status, Is.EqualTo(MessageStatus.Successful));
            Assert.That(row.SendingEndpointName, Is.EqualTo(audit.SendingEndpoint.Name));
            Assert.That(row.SendingEndpointHost, Is.EqualTo(audit.SendingEndpoint.Host));
            Assert.That(row.SendingEndpointHostId, Is.EqualTo(audit.SendingEndpoint.HostId));
            Assert.That(row.ReceivingEndpointName, Is.EqualTo(audit.ReceivingEndpoint.Name));
            Assert.That(row.ReceivingEndpointHost, Is.EqualTo(audit.ReceivingEndpoint.Host));
            Assert.That(row.ReceivingEndpointHostId, Is.EqualTo(audit.ReceivingEndpoint.HostId));
            Assert.That(row.CriticalTimeTicks, Is.EqualTo((audit.ProcessingEnded - audit.TimeSent).Ticks));
            Assert.That(row.ProcessingTimeTicks, Is.EqualTo((audit.ProcessingEnded - audit.ProcessingStarted).Ticks));
            Assert.That(row.DeliveryTimeTicks, Is.EqualTo((audit.ProcessingStarted - audit.TimeSent).Ticks));
            Assert.That(MessageHeaders.Read(row.HeadersJson), Is.EqualTo(audit.Headers));
            Assert.That(row.BodyContentType, Is.EqualTo(audit.ContentType));
            Assert.That(row.BodySize, Is.EqualTo(audit.Body.Length));
        }
    }

    [Test]
    public async Task Timestamps_are_read_back_as_utc()
    {
        var audit = new IngestedAudit();

        await IngestAudit(audit);

        var row = await GetAuditMessage(audit.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.CreatedOn.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.ProcessedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.TimeSent.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    [Test]
    public async Task A_successfully_retried_message_is_recorded_as_resolved_successfully()
    {
        var failedMessageId = Guid.NewGuid();
        var audit = new IngestedAudit { RetryOf = failedMessageId.ToString() };

        await IngestAudit(audit);

        var row = await GetAuditMessage(failedMessageId);

        Assert.That(row.Status, Is.EqualTo(MessageStatus.ResolvedSuccessfully));
    }

    // RavenDB deduplicates a redelivered audit message on its document id. The relational persisters
    // deliberately do not, because that would put an index probe on every insert of the hot path.
    [Test]
    public async Task A_redelivered_audit_message_produces_a_second_row()
    {
        var audit = new IngestedAudit();

        await IngestAudit(audit);
        await IngestAudit(audit);

        var rows = await GetAuditMessages(audit.UniqueMessageId);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows.Select(row => row.Id), Is.Unique);
    }

    [Test]
    public async Task A_batch_larger_than_one_statement_is_written_in_full()
    {
        var audits = Enumerable.Range(0, 120).Select(_ => new IngestedAudit()).ToArray();

        await IngestAudit(audits);

        Assert.That(await CountAuditMessages(), Is.EqualTo(audits.Length));
    }

    [Test]
    public async Task Audit_rows_and_known_endpoints_commit_in_one_batch()
    {
        var audit = new IngestedAudit();
        var endpoint = new KnownEndpoint { EndpointDetails = audit.ReceivingEndpoint };

        await InBatch(async unitOfWork =>
        {
            await unitOfWork.Audit.RecordProcessedMessage(audit.ToProcessedMessage(), audit.Body);
            await unitOfWork.Monitoring.RecordKnownEndpoint(endpoint);
        });

        var knownEndpoints = await GetKnownEndpoints([audit.ReceivingEndpoint.GetDeterministicId()]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await GetAuditMessages(audit.UniqueMessageId), Has.Count.EqualTo(1));
            Assert.That(knownEndpoints, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Failed_and_audited_messages_share_one_batch()
    {
        var failure = new IngestedFailure();
        var audit = new IngestedAudit();

        await InBatch(async unitOfWork =>
        {
            await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            await unitOfWork.Audit.RecordProcessedMessage(audit.ToProcessedMessage(), audit.Body);
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(failure.UniqueMessageId), Is.Not.Null);
            Assert.That(await GetAuditMessages(audit.UniqueMessageId), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task An_empty_batch_writes_nothing()
    {
        await InBatch(_ => Task.CompletedTask);

        Assert.That(await CountAuditMessages(), Is.Zero);
    }
}
