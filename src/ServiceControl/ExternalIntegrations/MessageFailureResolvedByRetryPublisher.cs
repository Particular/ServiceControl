namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Raven.Client;

    class MessageFailureResolvedByRetryPublisher : EventPublisher<MessageFailureResolvedByRetry, MessageFailureResolvedByRetryPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageFailureResolvedByRetry @event)
        {
            return new DispatchContext
            {
                FailedMessageId = new Guid(@event.FailedMessageId)
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.MessageFailureResolvedByRetry
            {
                FailedMessageId = r.FailedMessageId.ToString(),
                AlternativeFailedMessageIds = r.AlternativeFailedMessageIds
            }));
        }

        public class DispatchContext
        {
            public Guid FailedMessageId { get; set; }
            public string[] AlternativeFailedMessageIds { get; set; }
        }
    }
}