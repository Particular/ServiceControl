namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using ExternalIntegrations;
    using Raven.Client;

    class MessageFailureResolvedByRetryPublisher : EventPublisher<MessageFailureResolvedByRetryDomainEvent, MessageFailureResolvedByRetryPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageFailureResolvedByRetryDomainEvent @event)
        {
            return new DispatchContext
            {
                FailedMessageId = @event.FailedMessageId
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.MessageFailureResolvedByRetry
            {
                FailedMessageId = r.FailedMessageId,
                AlternativeFailedMessageIds = r.AlternativeFailedMessageIds
            }));
        }

        public class DispatchContext
        {
            public string FailedMessageId { get; set; }
            public string[] AlternativeFailedMessageIds { get; set; }
        }
    }
}