namespace ServiceControl.MessageAuditing.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Contracts.Operations;

    public class FailedMessageStateUpdateHandler : 
        IHandleMessages<MessageFailureResolvedByRetry>,
        IHandleMessages<FailedMessageArchived>

    {
        public IDocumentSession Session { get; set; }

        public void Handle(MessageFailureResolvedByRetry message)
        {
            var failedMessage = Session.Load<FailedMessage>(new Guid(message.FailedMessageId));

            if (failedMessage != null)
            {
                Session.Delete(failedMessage);
            }
        }

        public void Handle(FailedMessageArchived message)
        {
            var failedMessage = Session.Load<FailedMessage>(new Guid(message.FailedMessageId));

            if (failedMessage == null)
            {
                return; //No point throwing
            }

            failedMessage.Status = MessageStatus.ArchivedFailure;
        }
    }
}