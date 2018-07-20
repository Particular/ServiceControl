namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using Raven.Client;

    class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public SagaUpdatedHandler(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            var sagaHistory = new SagaSnapshot
            {
                SagaId = message.SagaId,
                SagaType = message.SagaType,
                FinishTime = message.FinishTime,
                StartTime = message.StartTime,
                StateAfterChange = message.SagaState,
                Endpoint = message.Endpoint,
                InitiatingMessage = CreateInitiatingMessage(message.Initiator)
            };

            if (message.IsNew)
            {
                sagaHistory.Status = SagaStateChangeStatus.New;
            }
            else
            {
                sagaHistory.Status = SagaStateChangeStatus.Updated;
            }

            if (message.IsCompleted)
            {
                sagaHistory.Status = SagaStateChangeStatus.Completed;
            }

            sagaHistory.ProcessedAt = message.FinishTime;

            AddResultingMessages(message.ResultingMessages, sagaHistory);

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(sagaHistory).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        static InitiatingMessage CreateInitiatingMessage(SagaChangeInitiator initiator)
        {
            return new InitiatingMessage
            {
                MessageId = initiator.InitiatingMessageId,
                IsSagaTimeoutMessage = initiator.IsSagaTimeoutMessage,
                OriginatingEndpoint = initiator.OriginatingEndpoint,
                OriginatingMachine = initiator.OriginatingMachine,
                TimeSent = initiator.TimeSent,
                MessageType = initiator.MessageType,
                Intent = initiator.Intent,
            };
        }

        static void AddResultingMessages(List<SagaChangeOutput> sagaChangeResultingMessages, SagaSnapshot sagaStateChange)
        {
            foreach (var toAdd in sagaChangeResultingMessages)
            {
                var resultingMessage = sagaStateChange.OutgoingMessages.FirstOrDefault(x => x.MessageId == toAdd.ResultingMessageId);
                if (resultingMessage == null)
                {
                    resultingMessage = new ResultingMessage();
                    sagaStateChange.OutgoingMessages.Add(resultingMessage);
                }
                resultingMessage.MessageType = toAdd.MessageType;
                resultingMessage.MessageId = toAdd.ResultingMessageId;
                resultingMessage.TimeSent = toAdd.TimeSent;
                resultingMessage.DeliveryDelay = toAdd.DeliveryDelay;
                resultingMessage.DeliverAt = toAdd.DeliveryAt;
                resultingMessage.Destination = toAdd.Destination;
                resultingMessage.Intent = toAdd.Intent;
            }
        }

        readonly IDocumentStore store;
    }
}