namespace ServiceBus.Management.Satellites
{
    using System;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;

    public class ErrorImportSatellite : ISatellite
    {
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = new Message(message)
                     {
                         FailureDetails = new FailureDetails(message),
                         Status = MessageStatus.Failed
                     };

                try
                {
                    session.Store(failedMessage);

                    session.SaveChanges();
                }
                catch (ConcurrencyException) //there is already a message in the store with the same id
                {
                    session.Advanced.Clear();
                    UpdateExistingMessage(session, failedMessage.Id, message);
                }
            }

            return true;
        }

        void UpdateExistingMessage(IDocumentSession session, string id, TransportMessage message)
        {
            var failedMessage = session.Load<Message>(id);
            
            var timeOfFailure = DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"]);

            if (failedMessage.FailureDetails.TimeOfFailure == timeOfFailure)
            {
                return;
            }

            if (failedMessage.Status == MessageStatus.Successful && timeOfFailure > failedMessage.ProcessedAt)
            {
                throw new InvalidOperationException("A message can't first be processed successfully and then fail, Id: " + failedMessage.Id);
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

        public void Start()
        {
            Logger.InfoFormat("Error import is now started, feeding error messages from: {0}", InputAddress);
        }

        public void Stop()
        {

        }

        public Address InputAddress
        {
            get { return Settings.ErrorQueue; }
        }

        public bool Disabled
        {
            get
            {
                return InputAddress == Address.Undefined;
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorImportSatellite));

    }
}