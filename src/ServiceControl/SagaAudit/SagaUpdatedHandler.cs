namespace ServiceControl.Operations.SagaState
{
    using System.Collections.Generic;
    using System.Linq;
    using EndpointPlugin.Messages.SagaState;
    using Infrastructure;
    using NServiceBus;
    using Raven.Client;

    public class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        public void Handle(SagaUpdatedMessage message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var id = DeterministicGuid.MakeId(message.Endpoint, message.SagaId.ToString());
                var sagaHistory = session.Load<SagaHistory>(id);

                if (sagaHistory == null)
                {
                    sagaHistory = new SagaHistory
                        {
                            Id = id,
                            SagaId = message.SagaId,
                        };
                }

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
                sagaStateChange.InitiatingMessage = CreateInitiatingMessage(message.Initiator);

                AddResultingMessages(message.ResultingMessages, sagaStateChange);

                session.Store(sagaHistory);
                session.SaveChanges();
            }
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
                    resultingMessage = new ResultingMessage();
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