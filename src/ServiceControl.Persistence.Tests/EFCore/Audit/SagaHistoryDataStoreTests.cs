namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.SagaAudit;

class SagaHistoryDataStoreTests : AuditIngestionTestBase
{
    ISagaHistoryDataStore History => ServiceProvider.GetRequiredService<ISagaHistoryDataStore>();

    [Test]
    public async Task Pages_a_sagas_changes_newest_first_and_reports_the_total()
    {
        var sagaId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        await IngestSnapshots(Snapshot(sagaId, start, SagaStateChangeStatus.New), Snapshot(sagaId, start.AddMinutes(1), SagaStateChangeStatus.Updated), Snapshot(sagaId, start.AddMinutes(2), SagaStateChangeStatus.Completed));
        await IngestSnapshots(Snapshot(Guid.NewGuid(), start, SagaStateChangeStatus.New));

        var firstPage = await History.QuerySagaHistoryById(sagaId, new PagingInfo(1, 2));
        var secondPage = await History.QuerySagaHistoryById(sagaId, new PagingInfo(2, 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPage.Results.SagaId, Is.EqualTo(sagaId));
            Assert.That(firstPage.Results.SagaType, Is.EqualTo("MyCompany.Sales.OrderSaga"));
            Assert.That(firstPage.Results.Changes.Select(change => change.Status), Is.EqualTo(new[] { SagaStateChangeStatus.Completed, SagaStateChangeStatus.Updated }));
            Assert.That(firstPage.QueryStats.TotalCount, Is.EqualTo(3));
            Assert.That(secondPage.Results.Changes.Select(change => change.Status), Is.EqualTo(new[] { SagaStateChangeStatus.New }));
            Assert.That(secondPage.QueryStats.TotalCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Round_trips_a_changes_messages()
    {
        var sagaId = Guid.NewGuid();
        var snapshot = Snapshot(sagaId, new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc), SagaStateChangeStatus.New);
        snapshot.InitiatingMessage = new InitiatingMessage { MessageId = "m1", OriginatingEndpoint = "Ordering", MessageType = "OrderPlaced", TimeSent = snapshot.StartTime, Intent = "Send" };
        snapshot.OutgoingMessages = [new ResultingMessage { MessageId = "m2", Destination = "Billing", MessageType = "BillOrder", TimeSent = snapshot.FinishTime, Intent = "Send" }];

        await IngestSnapshots(snapshot);

        var change = (await History.QuerySagaHistoryById(sagaId, new PagingInfo())).Results.Changes.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(change.InitiatingMessage.MessageId, Is.EqualTo("m1"));
            Assert.That(change.InitiatingMessage.OriginatingEndpoint, Is.EqualTo("Ordering"));
            Assert.That(change.OutgoingMessages.Single().Destination, Is.EqualTo("Billing"));
            Assert.That(change.StateAfterChange, Is.EqualTo(snapshot.StateAfterChange));
            Assert.That(change.Endpoint, Is.EqualTo(snapshot.Endpoint));
        }
    }

    [Test]
    public async Task An_unknown_saga_has_no_history()
    {
        var result = await History.QuerySagaHistoryById(Guid.NewGuid(), new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Is.Null);
            Assert.That(result.QueryStats.TotalCount, Is.Zero);
        }
    }

    static SagaSnapshot Snapshot(Guid sagaId, DateTime finishTime, SagaStateChangeStatus status) => new()
    {
        SagaId = sagaId,
        SagaType = "MyCompany.Sales.OrderSaga",
        Status = status,
        StartTime = finishTime.AddSeconds(-1),
        FinishTime = finishTime,
        ProcessedAt = finishTime,
        Endpoint = "Sales",
        StateAfterChange = "{\"OrderId\":42}"
    };
}
