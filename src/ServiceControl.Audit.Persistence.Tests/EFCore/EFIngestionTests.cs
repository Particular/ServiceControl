namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Monitoring;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.SagaAudit;

    class EFIngestionTests : EFPersistenceTestFixture
    {
        [Test]
        public async Task Reports_what_was_recorded()
        {
            var message = MakeMessage(isRetried: true, extraHeaders: new Dictionary<string, string>
            {
                ["NServiceBus.InvokedSagas"] = $"Shipping.ShippingPolicy:{SagaId}",
                ["ServiceControl.SagaStateChange"] = $"{SagaId}:Updated"
            });

            await Ingest(message);

            var view = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(view.Id, Is.EqualTo(message.UniqueMessageId));
                Assert.That(view.MessageId, Is.EqualTo(message.Headers[Headers.MessageId]));
                Assert.That(view.MessageType, Is.EqualTo(MessageType));
                Assert.That(view.TimeSent, Is.EqualTo(TimeSent));
                Assert.That(view.TimeSent!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(view.ProcessedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(view.Status, Is.EqualTo(MessageStatus.ResolvedSuccessfully));
                Assert.That(view.MessageIntent, Is.EqualTo(MessageIntent.Publish));
                Assert.That(view.SendingEndpoint.Name, Is.EqualTo("Sender"));
                Assert.That(view.SendingEndpoint.HostId, Is.EqualTo(SenderHostId));
                Assert.That(view.ReceivingEndpoint.Host, Is.EqualTo("receiver-host"));
                Assert.That(view.CriticalTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(view.ProcessingTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(view.DeliveryTime, Is.EqualTo(TimeSpan.FromSeconds(2)));
                Assert.That(view.Headers, Does.Contain(new KeyValuePair<string, string>(Headers.ProcessingEndpoint, "Receiver")));
                Assert.That(view.InvokedSagas.Single().SagaId, Is.EqualTo(SagaId));
                Assert.That(view.InvokedSagas.Single().ChangeStatus, Is.EqualTo("Updated"));
            }
        }

        [Test]
        public async Task Stores_a_redelivered_message_again()
        {
            var message = MakeMessage();

            await Ingest(message);
            await Ingest(MakeMessage(messageId: message.Headers[Headers.MessageId]));

            var result = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Writes_a_batch_larger_than_one_statement()
        {
            await Ingest([.. Enumerable.Range(0, 120).Select(_ => MakeMessage())]);

            var result = await MessagesViewStore.GetMessages(true, new PagingInfo(pageSize: 200), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.QueryStats.TotalCount, Is.EqualTo(120));
                Assert.That(result.Results, Has.Count.EqualTo(120));
            }
        }

        [Test]
        public async Task Writes_nothing_for_a_batch_that_is_not_completed()
        {
            await using (var unitOfWork = await StartAuditUnitOfWork(1))
            {
                await unitOfWork.RecordProcessedMessage(MakeMessage());
            }

            var stored = await WithDbContext((dbContext, token) => dbContext.AuditMessages.CountAsync(token));

            Assert.That(stored, Is.Zero);
        }

        [Test]
        public async Task Stores_a_redelivered_saga_snapshot_again()
        {
            var sagaId = Guid.NewGuid();

            for (var delivery = 0; delivery < 2; delivery++)
            {
                await using var unitOfWork = await StartAuditUnitOfWork(1);
                await unitOfWork.RecordSagaSnapshot(new SagaSnapshot
                {
                    SagaId = sagaId,
                    SagaType = "Shipping.ShippingPolicy",
                    Status = SagaStateChangeStatus.Updated,
                    StartTime = TimeSent,
                    FinishTime = TimeSent.AddSeconds(1),
                    InitiatingMessage = new InitiatingMessage { MessageId = "initiating", MessageType = MessageType, TimeSent = TimeSent },
                    OutgoingMessages = [new ResultingMessage { MessageId = "outgoing", Destination = "Billing", TimeSent = TimeSent }]
                });
                await unitOfWork.Complete();
            }

            var history = (await SagaHistoryStore.QuerySagaHistoryById(sagaId)).Results;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(history.Changes, Has.Count.EqualTo(2));
                Assert.That(history.Changes[0].InitiatingMessage.MessageId, Is.EqualTo("initiating"));
                Assert.That(history.Changes[0].OutgoingMessages.Single().Destination, Is.EqualTo("Billing"));
            }
        }

        static readonly Guid SagaId = Guid.NewGuid();
    }
}
