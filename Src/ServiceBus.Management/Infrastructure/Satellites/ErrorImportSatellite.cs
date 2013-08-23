namespace ServiceBus.Management.Infrastructure.Satellites
{
    using System;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Settings;

    public class ErrorImportSatellite : ISatellite
    {
        public IDocumentStore Store { get; set; }

        public ISendMessages Forwarder { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = new Message(message)
                {
                    FailureDetails = new FailureDetails(message),
                    Status = MessageStatus.Failed,
                    ReplyToAddress = message.ReplyToAddress.ToString()
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

            Forwarder.Send(message, Settings.ErrorLogQueue);

            return true;
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
            get { return InputAddress == Address.Undefined; }
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

        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorImportSatellite));
    }
}