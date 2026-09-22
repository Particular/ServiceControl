namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.SagaAudit;

class SagaSnapshotIngestionTests : AuditIngestionTestBase
{
    [Test]
    public async Task Writes_every_mapped_column_of_a_saga_snapshot()
    {
        var snapshot = Snapshot();

        await IngestSnapshots(snapshot);

        var row = (await GetSagaSnapshots(snapshot.SagaId)).Single();
        var initiatingMessage = SagaSnapshotJson.ReadInitiatingMessage(row.InitiatingMessageJson);
        var outgoingMessages = SagaSnapshotJson.ReadOutgoingMessages(row.OutgoingMessagesJson);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.CreatedOn, Is.EqualTo(IngestionHour));
            Assert.That(row.SagaType, Is.EqualTo(snapshot.SagaType));
            Assert.That(row.Status, Is.EqualTo(snapshot.Status));
            Assert.That(row.StartTime, Is.EqualTo(snapshot.StartTime));
            Assert.That(row.FinishTime, Is.EqualTo(snapshot.FinishTime));
            Assert.That(row.ProcessedAt, Is.EqualTo(snapshot.ProcessedAt));
            Assert.That(row.Endpoint, Is.EqualTo(snapshot.Endpoint));
            Assert.That(row.StateAfterChange, Is.EqualTo(snapshot.StateAfterChange));
            Assert.That(initiatingMessage.MessageId, Is.EqualTo(snapshot.InitiatingMessage.MessageId));
            Assert.That(initiatingMessage.OriginatingEndpoint, Is.EqualTo(snapshot.InitiatingMessage.OriginatingEndpoint));
            Assert.That(initiatingMessage.TimeSent, Is.EqualTo(snapshot.InitiatingMessage.TimeSent));
            Assert.That(initiatingMessage.IsSagaTimeoutMessage, Is.True);
            Assert.That(outgoingMessages, Has.Count.EqualTo(2));
            Assert.That(outgoingMessages.Select(m => m.Destination), Is.EqualTo(snapshot.OutgoingMessages.Select(m => m.Destination)));
            Assert.That(outgoingMessages[0].DeliveryDelay, Is.EqualTo(snapshot.OutgoingMessages[0].DeliveryDelay));
            Assert.That(outgoingMessages[1].DeliverAt, Is.EqualTo(snapshot.OutgoingMessages[1].DeliverAt));
        }
    }

    [Test]
    public async Task Times_without_a_kind_are_stored_as_utc()
    {
        var snapshot = Snapshot();
        snapshot.StartTime = DateTime.SpecifyKind(snapshot.StartTime, DateTimeKind.Unspecified);
        snapshot.FinishTime = DateTime.SpecifyKind(snapshot.FinishTime, DateTimeKind.Unspecified);
        snapshot.ProcessedAt = DateTime.SpecifyKind(snapshot.ProcessedAt, DateTimeKind.Unspecified);

        await IngestSnapshots(snapshot);

        var row = (await GetSagaSnapshots(snapshot.SagaId)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.StartTime, Is.EqualTo(snapshot.StartTime));
            Assert.That(row.StartTime.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.FinishTime.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.ProcessedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    // Snapshots carry no natural key, so a redelivered saga audit message adds a second step. This
    // is asserted because it differs from RavenDB, which assigns the document id itself.
    [Test]
    public async Task A_redelivered_saga_audit_message_produces_a_second_snapshot()
    {
        var snapshot = Snapshot();

        await IngestSnapshots(snapshot);
        await IngestSnapshots(snapshot);

        Assert.That(await GetSagaSnapshots(snapshot.SagaId), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task A_snapshot_without_an_initiating_message_round_trips()
    {
        var snapshot = Snapshot();
        snapshot.InitiatingMessage = null;
        snapshot.OutgoingMessages.Clear();

        await IngestSnapshots(snapshot);

        var row = (await GetSagaSnapshots(snapshot.SagaId)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.InitiatingMessageJson, Is.Null);
            Assert.That(SagaSnapshotJson.ReadOutgoingMessages(row.OutgoingMessagesJson), Is.Empty);
        }
    }

    static SagaSnapshot Snapshot() => new()
    {
        SagaId = Guid.NewGuid(),
        SagaType = "MyCompany.Sales.OrderSaga",
        Status = SagaStateChangeStatus.Updated,
        StartTime = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc),
        FinishTime = new DateTime(2026, 7, 22, 10, 0, 2, DateTimeKind.Utc),
        ProcessedAt = new DateTime(2026, 7, 22, 10, 0, 2, DateTimeKind.Utc),
        Endpoint = "Sales",
        StateAfterChange = "{\"OrderId\":42}",
        InitiatingMessage = new InitiatingMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            IsSagaTimeoutMessage = true,
            OriginatingEndpoint = "Ordering",
            OriginatingMachine = "SenderHost",
            TimeSent = new DateTime(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc),
            MessageType = "MyCompany.Sales.OrderPlaced",
            Intent = "Send"
        },
        OutgoingMessages =
        [
            new ResultingMessage { MessageId = Guid.NewGuid().ToString(), Destination = "Billing", TimeSent = new DateTime(2026, 7, 22, 10, 0, 1, DateTimeKind.Utc), MessageType = "MyCompany.Billing.BillOrder", Intent = "Send", DeliveryDelay = TimeSpan.FromMinutes(5) },
            new ResultingMessage { MessageId = Guid.NewGuid().ToString(), Destination = "Sales", TimeSent = new DateTime(2026, 7, 22, 10, 0, 1, DateTimeKind.Utc), MessageType = "MyCompany.Sales.OrderTimeout", Intent = "Send", DeliverAt = new DateTime(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc) }
        ]
    };
}
