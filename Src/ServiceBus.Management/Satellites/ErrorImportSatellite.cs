namespace ServiceBus.Management.Satellites
{
    using System;
    using NServiceBus;
    using NServiceBus.Satellites;
    using Raven.Client;

    public class ErrorImportSatellite : ISatellite
    {
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {
                var failedMessage = session.Load<Message>(message.IdForCorrelation);
                var timeOfFailure = DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"]);

                if (failedMessage == null)
                {
                    failedMessage = new Message(message)
                    {
                        FailureDetails = new FailureDetails(message),
                        Status = MessageStatus.Failed
                    };
                }
                else
                {
                    if (failedMessage.FailureDetails.TimeOfFailure == timeOfFailure)
                    {
                        return true;//duplicate
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
                }

                session.Store(failedMessage);

                session.SaveChanges();
            }

            return true;
        }


       

        public void Start()
        {

        }

        public void Stop()
        {

        }

        public Address InputAddress { get { return Address.Parse("error"); } }

        public bool Disabled { get { return false; } }
    }
}