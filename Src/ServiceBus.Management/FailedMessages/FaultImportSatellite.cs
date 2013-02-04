namespace ServiceBus.Management.FailedMessages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NServiceBus;
    using NServiceBus.Satellites;
    using Newtonsoft.Json;
    using Raven.Client;

    public class FaultImportSatellite : ISatellite
    {
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {

                var failedMessage = session.Load<Message>(message.IdForCorrelation);

                if (failedMessage == null)
                {
                    failedMessage = new Message(message)
                    {
                        FailureDetails = new FailureDetails
                        {
                            FailedInQueue = message.Headers["NServiceBus.FailedQ"],
                            TimeOfFailure =
                                DateTimeExtensions.ToUtcDateTime(message.Headers["NServiceBus.TimeOfFailure"])
                        },
                        Status = MessageStatus.Failed
                    };
                }
                else
                {
                    if (failedMessage.Status == MessageStatus.Successfull)
                        throw new InvalidOperationException("A message can't first be processed successfully and then fail, Id: " + failedMessage.Id);

                    failedMessage.Status = MessageStatus.RepeatedFailures;
                }



                failedMessage.FailureDetails.Exception = GetException(message);
                failedMessage.FailureDetails.NumberOfTimesFailed++;

                session.Store(failedMessage);

                session.SaveChanges();
            }

            return true;
        }


        ExceptionDetails GetException(TransportMessage message)
        {
            return new ExceptionDetails
                {
                    ExceptionType = message.Headers["NServiceBus.ExceptionInfo.ExceptionType"],
                    Message = message.Headers["NServiceBus.ExceptionInfo.Message"],
                    Source = message.Headers["NServiceBus.ExceptionInfo.Source"],
                    StackTrace = message.Headers["NServiceBus.ExceptionInfo.StackTrace"]
                };
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