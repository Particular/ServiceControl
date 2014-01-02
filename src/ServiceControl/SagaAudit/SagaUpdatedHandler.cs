namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Linq;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using Raven.Client;

    public class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public IDocumentSession Session { get; set; }

        public void Handle(SagaUpdatedMessage message)
        {

            var sagaHistory = Session.Load<SagaHistory>(message.SagaId) ?? new SagaHistory
            {
                Id = message.SagaId,
                SagaType = message.SagaType
            };

            var sagaStateChange = sagaHistory.Changes.FirstOrDefault(x => x.InitiatingMessage.InitiatingMessageId == message.Initiator.InitiatingMessageId);
            if (sagaStateChange == null)
            {
                sagaStateChange = new SagaStateChange();
                sagaHistory.Changes.Add(sagaStateChange);
            }

            sagaStateChange.FinishTime = message.FinishTime;
            sagaStateChange.StartTime = message.StartTime;
            sagaStateChange.StateAfterChange = message.SagaState;
            sagaStateChange.Endpoint = message.Endpoint;
            sagaStateChange.IsNew = message.IsNew;
            sagaStateChange.InitiatingMessage = CreateInitiatingMessage(message.Initiator);

            AddResultingMessages(message.ResultingMessages, sagaStateChange);

            Session.Store(sagaHistory);
        }

        static InitiatingMessage CreateInitiatingMessage(SagaChangeInitiator initiator)
        {
            return new InitiatingMessage
                {
                    InitiatingMessageId = initiator.InitiatingMessageId,
                    IsSagaTimeoutMessage = initiator.IsSagaTimeoutMessage,
                    OriginatingEndpoint = initiator.OriginatingEndpoint,
                    OriginatingMachine = initiator.OriginatingMachine,
                    TimeSent = initiator.TimeSent,
                    MessageType = initiator.MessageType,
                };
        }

        static void AddResultingMessages(List<SagaChangeOutput> sagaChangeResultingMessages, SagaStateChange sagaStateChange)
        {
            foreach (var toAdd in sagaChangeResultingMessages)
            {
                var resultingMessage = sagaStateChange.OutgoingMessages.FirstOrDefault(x => x.ResultingMessageId == toAdd.ResultingMessageId);
                if (resultingMessage == null)
                {
                    resultingMessage = new ResultingMessage
                        {
                            ProcessingState = ProcessingState.Pending
                        };
                    sagaStateChange.OutgoingMessages.Add(resultingMessage);
                }
                resultingMessage.MessageType = toAdd.MessageType;
                resultingMessage.ResultingMessageId = toAdd.ResultingMessageId;
                resultingMessage.TimeSent = toAdd.TimeSent;
                resultingMessage.DeliveryDelay = toAdd.DeliveryDelay;
                resultingMessage.Destination = toAdd.Destination;
            }
        }
    }
}