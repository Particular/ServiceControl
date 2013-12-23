namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    public class MessageCorrelationHandler : IHandleMessages<ImportFailedMessage>, IHandleMessages<ImportSuccessfullyProcessedMessage>
    {
        public IDocumentSession Session { get; set; }
        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            var headers = message.PhysicalMessage.Headers;
            Handle(headers, resultingMessage =>
            {
                var timeOfFailure = DateTimeExtensions.ToUtcDateTime(headers["NServiceBus.TimeOfFailure"]);
                resultingMessage.TimeProcessed = timeOfFailure;
                resultingMessage.ProcessingState = ProcessingState.Success;
            });
        }

        public void Handle(ImportFailedMessage message)
        {
            var headers = message.PhysicalMessage.Headers;
            Handle(headers, resultingMessage =>
            {
                var timeOfFailure = DateTimeExtensions.ToUtcDateTime(headers["NServiceBus.TimeOfFailure"]);
                //If we already received a success the we will ignore the failure
                if (resultingMessage.ProcessingState == ProcessingState.Success)
                {
                    return;
                }
                resultingMessage.TimeProcessed = timeOfFailure;
                resultingMessage.ProcessingState = ProcessingState.Failed;
            });
        }

        void Handle(Dictionary<string, string> headers, Action<ResultingMessage> updateResultingMessage)
        {

            Guid originatingSagaId;
            if (!IsSagaMessage(headers, out originatingSagaId))
            {
                return;
            }

            var id = "sagahistory/" + originatingSagaId;

            var sagaHistory = Session.Load<SagaHistory>(id) ?? new SagaHistory
            {
                Id = id,
                SagaId = originatingSagaId,
            };

            var relatedTo = headers[Headers.RelatedTo];
            var stateChange = sagaHistory.Changes.FirstOrDefault(x => x.InitiatingMessage.InitiatingMessageId == relatedTo);
            if (stateChange == null)
            {
                stateChange = new SagaStateChange
                    {
                        InitiatingMessage = new InitiatingMessage
                            {
                                InitiatingMessageId = relatedTo
                            }
                    };
                sagaHistory.Changes.Add(stateChange);
            }

            var messageId = headers[Headers.MessageId];
            var resultingMessage = stateChange.OutgoingMessages.FirstOrDefault(x => x.ResultingMessageId == messageId);
            if (resultingMessage == null)
            {
                resultingMessage = new ResultingMessage
                    {
                        ResultingMessageId = messageId,
                        MessageType = headers[Headers.EnclosedMessageTypes],
                        TimeSent = DateTimeExtensions.ToUtcDateTime(headers[Headers.TimeSent]),
                    };
                stateChange.OutgoingMessages.Add(resultingMessage);
            }

            updateResultingMessage(resultingMessage);
            Session.Store(sagaHistory);
        }

        static bool IsSagaMessage(Dictionary<string, string> headers, out Guid originatingSagaId)
        {
            string originatingSagaIdString;
            if (headers.TryGetValue(Headers.OriginatingSagaId, out originatingSagaIdString))
            {
                originatingSagaId = Guid.Parse(originatingSagaIdString);
                return true;
            }
            originatingSagaId = new Guid();
            return false;
        }
    }
}