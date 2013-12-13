namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class ImportFailedMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        public IDocumentStore DocumentStore { get; set; }
 
        public void Handle(ImportFailedMessage message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var messageId = message.UniqueMessageId;

                var failure = session.Load<FailedMessage>(messageId) ?? new FailedMessage
                {
                    Id = messageId
                };

                failure.Status = FailedMessageStatus.Unresolved;

                var timeOfFailure = message.FailureDetails.TimeOfFailure;

                //check for duplicate
                if (failure.ProcessingAttempts.Any(a =>a.AttemptedAt == timeOfFailure))
                {
                    return;
                }


               failure.ProcessingAttempts.Add(new FailedMessage.ProcessingAttempt
               {
                   AttemptedAt = timeOfFailure,
                   FailingEndpoint = message.ReceivingEndpoint,
                   FailureDetails =message.FailureDetails,
                   MessageMetadata = message.Metadata,
                   MessageId = message.PhysicalMessage.MessageId,
                   Headers = message.PhysicalMessage.Headers,
                   ReplyToAddress = message.PhysicalMessage.ReplyToAddress,
                   Recoverable = message.PhysicalMessage.Recoverable,
                   CorrelationId = message.PhysicalMessage.CorrelationId,
                   MessageIntent = message.PhysicalMessage.MessageIntent,
                   Body = message.PhysicalMessage.Body
               });

                //todo: sort the list in time order

                session.Store(failure);

                session.SaveChanges();
            }
        }
    }
}