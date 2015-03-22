namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolvedByRetry>
    {
        public IDocumentSession Session { get; set; }

        public void Handle(MessageFailureResolvedByRetry message)
        {
            var failedMessage = Session.Load<MessageFailureHistory>(new Guid(message.FailedMessageId));

            if (failedMessage == null)
            {
                return; //No point throwing
            }

            failedMessage.Status = FailedMessageStatus.Resolved;    
        }
    }
}