namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using Raven.Client;

    class MessageFailureResolvedDomainHandler : IDomainHandler<MessageFailureResolvedByRetry>
    {
        public MessageFailureResolvedDomainHandler(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task Handle(MessageFailureResolvedByRetry domainEvent)
        {
            if (await MarkMessageAsResolved(domainEvent.FailedMessageId)
                .ConfigureAwait(false))
            {
                return;
            }

            if (domainEvent.AlternativeFailedMessageIds == null)
            {
                return;
            }

            foreach (var alternative in domainEvent.AlternativeFailedMessageIds.Where(x => x != domainEvent.FailedMessageId))
            {
                if (await MarkMessageAsResolved(alternative)
                    .ConfigureAwait(false))
                {
                    return;
                }
            }
        }

        async Task<bool> MarkMessageAsResolved(string failedMessageId)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    return false;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                await session.SaveChangesAsync().ConfigureAwait(false);

                return true;
            }
        }

        IDocumentStore store;
    }
}