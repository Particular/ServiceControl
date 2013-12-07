namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class ErrorMessageReceivedHandler : IHandleMessages<FailedMessageDetected>
    {
        public IDocumentStore DocumentStore { get; set; }
 
        public void Handle(FailedMessageDetected message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failure = session.Load<FailedMessage>(message.FailedMessageId) ?? new FailedMessage
                {
                    Id = message.FailedMessageId,
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
                   FailureDetails =message.FailureDetails,
                   Message = message.PhysicalMessage
               });

                //todo: sort the list in time order

                session.Store(failure);

                session.SaveChanges();
            }
        }
    }
}