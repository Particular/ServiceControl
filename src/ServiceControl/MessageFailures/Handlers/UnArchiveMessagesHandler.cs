namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public class UnArchiveMessagesHandler : IHandleMessages<UnArchiveMessages>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;

        public UnArchiveMessagesHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void Handle(UnArchiveMessages messages)
        {
            HandleAsync(messages).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(UnArchiveMessages messages)
        {
            FailedMessage[] failedMessages;

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                failedMessages = await session.LoadAsync<FailedMessage>(messages.FailedMessageIds.Select(FailedMessage.MakeDocumentId))
                    .ConfigureAwait(false);

                foreach (var failedMessage in failedMessages)
                {
                    if (failedMessage.Status == FailedMessageStatus.Archived)
                    {
                        failedMessage.Status = FailedMessageStatus.Unresolved;
                    }
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                MessagesCount = failedMessages.Length
            }).ConfigureAwait(false);
        }
    }
}