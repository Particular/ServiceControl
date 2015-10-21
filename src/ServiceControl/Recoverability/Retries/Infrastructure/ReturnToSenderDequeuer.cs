namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;
    using NServiceBus.Faults;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using Raven.Client;
    using ServiceControl.MessageFailures;

    class ReturnToSenderDequeuer : AdvancedDequeuer
    {
        readonly ISendMessages sender;
        CaptureIfMessageSendingFails faultManager;
        
        public ReturnToSenderDequeuer(ISendMessages sender, IDocumentStore store, IBus bus)
        {
            this.sender = sender;

            faultManager = new CaptureIfMessageSendingFails(store, bus, ExecuteOnFailure);
        }

        protected override void HandleMessage(TransportMessage message)
        {
            var destination = message.Headers["ServiceControl.TargetEndpointAddress"];
            var destinationAddress = Address.Parse(destination);

            message.Headers.Remove("ServiceControl.TargetEndpointAddress");
            message.Headers.Remove("ServiceControl.Retry.StagingId");

            try
            {
                sender.Send(message, destinationAddress);
            }
            catch (Exception)
            {
                message.Headers["ServiceControl.TargetEndpointAddress"] = destination;

                throw;
            }
        }

        protected override IManageMessageFailures FaultManager
        {
            get { return faultManager; }
        }

        class CaptureIfMessageSendingFails : IManageMessageFailures
        {
            static ILog Log = LogManager.GetLogger(typeof(CaptureIfMessageSendingFails));
            private IDocumentStore store;
            private IBus bus;
            readonly Action executeOnFailure;

            public CaptureIfMessageSendingFails(IDocumentStore store, IBus bus, Action executeOnFailure)
            {
                this.store = store;
                this.bus = bus;
                this.executeOnFailure = executeOnFailure;
            }

            public void SerializationFailedForMessage(TransportMessage message, Exception e)
            {
            }

            public void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e)
            {
                try
                {
                    var destination = message.Headers["ServiceControl.TargetEndpointAddress"];
                    var messageUniqueId = message.Headers["ServiceControl.Retry.UniqueMessageId"];
                    Log.Warn(string.Format("Failed to send '{0}' message to '{1}' for retry. Attempting to revert message status to unresolved so it can be tried again.", messageUniqueId, destination), e);

                    using (var session = store.OpenSession())
                    {
                        var failedMessage = session.Load<FailedMessage>(FailedMessage.MakeDocumentId(messageUniqueId));
                        if (failedMessage != null)
                        {
                            failedMessage.Status = FailedMessageStatus.Unresolved;
                        }
                        
                        var failedMessageRetry = session.Load<FailedMessageRetry>(FailedMessageRetry.MakeDocumentId(messageUniqueId));
                        if (failedMessageRetry != null)
                        {
                            session.Delete(failedMessageRetry);
                        }

                        session.SaveChanges();
                    }

                    bus.Publish<MessagesSubmittedForRetryFailed>(m =>
                    {
                        m.FailedMessageId = messageUniqueId;
                        m.Destination = destination;
                        try
                        {
                            m.Reason = e.GetBaseException().Message;
                        }
                        catch (Exception)
                        {
                            m.Reason = "Failed to retrieve reason!";
                        }

                    });
                }
                catch (Exception ex)
                {
                    // If something goes wrong here we just ignore, not the end of the world!
                    Log.Error("A failure occurred when trying to handle a retry failure.", ex);
                }
                finally
                {
                    executeOnFailure();
                }
            }

            public void Init(Address address)
            {
            }
        }
    }
}