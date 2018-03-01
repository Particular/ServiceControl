namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
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
            FailedMessage[] failedMessages;

            using (var session = store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                failedMessages = session.Load<FailedMessage>(messages.FailedMessageIds.Select(FailedMessage.MakeDocumentId));

                foreach (var failedMessage in failedMessages)
                {
                    if (failedMessage.Status == FailedMessageStatus.Archived)
                    {
                        failedMessage.Status = FailedMessageStatus.Unresolved;
                    }
                }

                session.SaveChanges();
            }

            domainEvents.Raise(new FailedMessagesUnArchived
            {
                MessagesCount = failedMessages.Length
            });
        }
    }
}