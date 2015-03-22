namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(ArchiveMessage message)
        {
            var failedMessage = Session.Load<MessageFailureHistory>(new Guid(message.FailedMessageId));

            if (failedMessage == null)
            {
                return; //No point throwing
            }

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                failedMessage.Status = FailedMessageStatus.Archived;

                Bus.Publish<FailedMessageArchived>(m=>m.FailedMessageId = message.FailedMessageId);
            }
        }
    }
}