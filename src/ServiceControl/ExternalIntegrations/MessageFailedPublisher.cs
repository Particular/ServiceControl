namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;

    public class MessageFailedPublisher : EventPublisher<MessageFailed, MessageFailedPublisher.Reference>
    {
        protected override Reference CreateReference(MessageFailed evnt)
        {
            return new Reference
            {
                FailedMessageId = new Guid(evnt.FailedMessageId)
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<Reference> references, IDocumentSession session)
        {
            var documentIds = references.Select(x => x.FailedMessageId).Cast<ValueType>().ToArray();
            var failedMessageData = session.Load<FailedMessage>(documentIds);
            return failedMessageData.Select(x => x.ToEvent());
        }

        public class Reference
        {
            public Guid FailedMessageId { get; set; }
        }

        
    }
}