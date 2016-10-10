namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Operations.BodyStorage;

    public class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly IMessageBodyStore messageBodyStore;

        public ArchiveMessageHandler(IBus bus, IDocumentStore store, IMessageBodyStore messageBodyStore)
        {
            this.bus = bus;
            this.store = store;
            this.messageBodyStore = messageBodyStore;
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

                    messageBodyStore.ChangeTag(failedMessage.UniqueMessageId, BodyStorageTags.ErrorPersistent, BodyStorageTags.ErrorTransient);

                    bus.Publish<FailedMessageArchived>(m => m.FailedMessageId = message.FailedMessageId);
                }

                session.SaveChanges();
            }
        }
    }
}