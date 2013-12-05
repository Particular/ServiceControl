namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using Contracts.MessageFailures;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceBus.Management.MessageAuditing;


    class ErrorMessageHandler : IHandleMessages<ErrorMessageReceived>
    {
        public IDocumentStore Store { get; set; }
        public IBus Bus { get; set; }

        public void Handle(ErrorMessageReceived message)
        {
            //using (var session = Store.OpenSession())
            //{
            //    session.Advanced.UseOptimisticConcurrency = true;

            //    var failedMessage = new Message(message)
            //    {
            //        FailureDetails = new FailureDetails(message.Headers),
            //        Status = MessageStatus.Failed,
            //        ReplyToAddress = message.ReplyToAddress
            //    };

            //    try
            //    {
            //        session.Store(failedMessage);
            //        session.SaveChanges();
            //        Bus.Publish<MessageFailed>(m =>
            //        {
            //            m.Id = failedMessage.Id;
            //            m.Endpoint = failedMessage.ReceivingEndpoint.Name;
            //            m.Machine = failedMessage.ReceivingEndpoint.Machine;
            //            m.FailedAt = failedMessage.TimeSent;
            //            m.Reason = failedMessage.FailureDetails.Exception.Message;
            //        });
            //    }
            //    catch (ConcurrencyException) //there is already a message in the store with the same id
            //    {
            //        session.Advanced.Clear();
            //        UpdateExistingMessage(session, failedMessage.Id, message.Headers);
            //    }
            //}
        }

        void UpdateExistingMessage(IDocumentSession session, string id, IDictionary<string,string> headers)
        {
            var message = session.Load<Message>(id);

            var timeOfFailure = DateTimeExtensions.ToUtcDateTime(headers["NServiceBus.TimeOfFailure"]);

            if (message.FailureDetails.TimeOfFailure == timeOfFailure)
            {
                return;
            }

            if (message.Status == MessageStatus.Successful && timeOfFailure > message.ProcessedAt)
            {
                throw new InvalidOperationException(
                    "A message can't first be processed successfully and then fail, Id: " + message.Id);
            }

            if (message.Status == MessageStatus.Successful)
            {
                message.FailureDetails = new FailureDetails(headers);
            }
            else
            {
                message.Status = MessageStatus.RepeatedFailure;

                message.FailureDetails.RegisterException(headers);
            }

            session.SaveChanges();
        }
    }
}
