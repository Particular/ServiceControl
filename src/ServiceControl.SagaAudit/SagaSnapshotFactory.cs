namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Linq;
    using EndpointPlugin.Messages.SagaState;

    public static class SagaSnapshotFactory
    {
        public static SagaSnapshot Create(SagaUpdatedMessage message)
        {
            var sagaSnapshot = new SagaSnapshot
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
                sagaSnapshot.Status = SagaStateChangeStatus.New;
            }
            else
            {
                sagaSnapshot.Status = SagaStateChangeStatus.Updated;
            }

            if (message.IsCompleted)
            {
                sagaSnapshot.Status = SagaStateChangeStatus.Completed;
            }

            sagaSnapshot.ProcessedAt = message.FinishTime;

            AddResultingMessages(message.ResultingMessages, sagaSnapshot);
            return sagaSnapshot;
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
                Intent = initiator.Intent
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
    }
}