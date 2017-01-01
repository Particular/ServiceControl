namespace ServiceControl.ExternalIntegrations
{
    using System;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;

    public class MessageFailedPublisher : EventPublisher<MessageFailed, Contracts.MessageFailed>
    {
        IDocumentStore store;

        public MessageFailedPublisher(IDocumentStore store)
        {
            this.store = store;
        }

        protected override Contracts.MessageFailed Convert(MessageFailed message)
        {
            FailedMessage failedMessageData;
            using (var session = store.OpenSession())
            {
                failedMessageData = session.Load<FailedMessage>(Guid.Parse(message.FailedMessageId));
            }
            return failedMessageData.ToEvent();
        }
    }
}