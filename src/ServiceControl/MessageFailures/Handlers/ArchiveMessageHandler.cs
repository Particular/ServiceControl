namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Threading.Tasks;
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

        public async Task Handle(ArchiveMessage message, IMessageHandlerContext context)
        {
            using (var session = store.OpenAsyncSession())
            {
                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(message.FailedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    return;
                }

                if (failedMessage.Status != FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Archived;

                    await domainEvents.Raise(new FailedMessageArchived
                    {
                        FailedMessageId = message.FailedMessageId
                    }).ConfigureAwait(false);
                }

                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}