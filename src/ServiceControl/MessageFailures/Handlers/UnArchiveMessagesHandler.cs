namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class UnArchiveMessagesHandler : IHandleMessages<UnArchiveMessages>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;

        public UnArchiveMessagesHandler(IBus bus, IDocumentStore store)
        {
            this.bus = bus;
            this.store = store;
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

            bus.Publish(new FailedMessagesUnArchived
            {
                MessagesCount = failedMessages.Length
            });
        }
    }
}