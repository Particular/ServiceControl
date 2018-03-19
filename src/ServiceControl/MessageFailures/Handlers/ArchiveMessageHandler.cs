namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;

        public ArchiveMessageHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
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

                    domainEvents.Raise(new FailedMessageArchived
                    {
                        FailedMessageId = message.FailedMessageId
                    });
                }

                session.SaveChanges();
            }
        }
    }
}