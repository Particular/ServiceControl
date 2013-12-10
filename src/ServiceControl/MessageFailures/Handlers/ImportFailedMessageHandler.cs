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

                var failure = session.Load<FailedMessage>(message.UniqueMessageId) ?? new FailedMessage
                {
                    Id = message.UniqueMessageId,
                    Status = MessageStatus.Failed,
                    MessageId = message.PhysicalMessage.MessageId
                };

                var timeOfFailure = message.FailureDetails.TimeOfFailure;

                //check for duplicate
                if (failure.ProcessingAttempts.Any(a => a.AttemptedAt == timeOfFailure))
                {
                    return;
                }

               failure.ProcessingAttempts.Add(new FailedMessage.ProcessingAttempt
               {
                   AttemptedAt = timeOfFailure,
                   FailingEndpoint = message.ReceivingEndpoint,
                   FailureDetails =message.FailureDetails,
                   Message = message.PhysicalMessage,
                   MessageProperties = message.Properties
               });

                //todo: sort the list in time order

                session.Store(failure);

                session.SaveChanges();
            }
        }
    }
}