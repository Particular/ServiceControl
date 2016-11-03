namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;

        public ArchiveMessageHandler(IBus bus, IDocumentStore store)
        {
            this.bus = bus;
            this.store = store;
        }

        public void Handle(ArchiveMessage message)
        {
            using (var session = store.OpenSession())
            {
                var failedMessage = session.Load<FailedMessage>(new Guid(message.FailedMessageId));

                if (failedMessage == null)
                {
                    return; //No point throwing
                }

                if (failedMessage.Status != FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Archived;

                    bus.Publish<FailedMessageArchived>(m => m.FailedMessageId = message.FailedMessageId);
                }

                session.SaveChanges();
            }
        }
    }
}