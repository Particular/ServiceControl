namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Operations.BodyStorage;

    public class UnArchiveMessagesHandler : IHandleMessages<UnArchiveMessages>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly IMessageBodyStore messageBodyStore;

        public UnArchiveMessagesHandler(IBus bus, IDocumentStore store, IMessageBodyStore messageBodyStore)
        {
            this.bus = bus;
            this.store = store;
            this.messageBodyStore = messageBodyStore;
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

            MarkMessageBodiesAsPersistent(messages.FailedMessageIds);

            bus.Publish(new FailedMessagesUnArchived
            {
                MessagesCount = failedMessages.Length
            });
        }

        private void MarkMessageBodiesAsPersistent(IEnumerable<string> messageBodyIds)
        {
            foreach (var messageBodyId in messageBodyIds)
            {
                messageBodyStore.ChangeTag(messageBodyId, BodyStorageTags.ErrorTransient, BodyStorageTags.ErrorPersistent);
            }
        }
    }
}