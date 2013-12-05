namespace ServiceControl.MessageFailures
{
    using System;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class ErrorMessageReceivedHandler : IHandleMessages<ErrorMessageReceived>
    {
        public IDocumentStore DocumentStore { get; set; }
 
        public void Handle(ErrorMessageReceived message)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failure = session.Load<FailedMessage>(message.ErrorMessageId) ?? new FailedMessage
                {
                    Id = message.ErrorMessageId,
                    MessageId = message.MessageId,
                    Status = MessageStatus.Failed
                };

                //todo check for duplicate using the timestamp

               failure.ProcessingAttempts.Add(new FailedMessage.ProcessingAttempt
               {
                   AttemptedAt = DateTime.UtcNow, //todo
                   FailureDetails =message.FailureDetails,
                   Message = new Message2(),//todo
               });


                session.Store(failure);

                session.SaveChanges();
            }
        }
    }
}