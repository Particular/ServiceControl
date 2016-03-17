namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class UnArchiveMessagesHandler : IHandleMessages<UnArchiveMessages>
    {
        public IDocumentSession Session { get; set; }

        public IBus Bus { get; set; }

        public void Handle(UnArchiveMessages messages)
        {
            var failedMessages = Session.Load<FailedMessage>(messages.FailedMessageIds.Select(FailedMessage.MakeDocumentId));

            foreach (var failedMessage in failedMessages)
            {
                if (failedMessage.Status == FailedMessageStatus.Archived)
                {
                    failedMessage.Status = FailedMessageStatus.Unresolved;
                }
            }

            Bus.Publish<FailedMessagesUnArchived>(m => m.MessagesCount = failedMessages.Length);
        }
    }
}