namespace ServiceControl.MessageFailures
{
    using System;
    using Infrastructure.Messages;
    using NServiceBus;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceBus.Management.MessageAuditing;

    class ErrorMessageHandler : IHandleMessages<ErrorMessageReceived>
    {
        public IDocumentStore Store { get; set; }
        
        public void Handle(ErrorMessageReceived message)
        {
            var transportMessage = message.MessageDetails;

            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
            
                var failedMessage = new Message(transportMessage)
                {
                    FailureDetails = new FailureDetails(transportMessage),
                    Status = MessageStatus.Failed,
                    ReplyToAddress = transportMessage.ReplyToAddress.ToString()
                };

                try
                {
                    session.Store(failedMessage);

                    session.SaveChanges();
                }
                catch (ConcurrencyException) //there is already a message in the store with the same id
                {
                    session.Advanced.Clear();
                    UpdateExistingMessage(session, failedMessage.Id, transportMessage);
                }
            }
        }

        void UpdateExistingMessage(IDocumentSession session, string id, ITransportMessage message)
        {
            var failedMessage = session.Load<Message>(id);

            var timeOfFailure = DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"]);

            if (failedMessage.FailureDetails.TimeOfFailure == timeOfFailure)
            {
                return;
            }

            if (failedMessage.Status == MessageStatus.Successful && timeOfFailure > failedMessage.ProcessedAt)
            {
                throw new InvalidOperationException(
                    "A message can't first be processed successfully and then fail, Id: " + failedMessage.Id);
            }

            if (failedMessage.Status == MessageStatus.Successful)
            {
                failedMessage.FailureDetails = new FailureDetails(message);
            }
            else
            {
                failedMessage.Status = MessageStatus.RepeatedFailure;

                failedMessage.FailureDetails.RegisterException(message);
            }

            session.SaveChanges();
        }

    
    }
}
