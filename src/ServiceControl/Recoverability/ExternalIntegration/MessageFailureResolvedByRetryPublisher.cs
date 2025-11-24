namespace ServiceControl.Recoverability.ExternalIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using ExternalIntegrations;

    class MessageEditedAndRetriedPublisher : EventPublisher<MessageEditedAndRetried,
        MessageEditedAndRetriedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageEditedAndRetried @event)
        {
            throw new System.NotImplementedException();
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts)
        {
            throw new System.NotImplementedException();
        }

        public class DispatchContext
        {
            public string FailedMessageId { get; set; }
            public string RetriedMessageId { get; set; }
            public string EditId { get; set; }
        }

    }
    class MessageFailureResolvedByRetryPublisher : EventPublisher<MessageFailureResolvedByRetry, MessageFailureResolvedByRetryPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(MessageFailureResolvedByRetry @event)
        {
            return new DispatchContext
            {
                FailedMessageId = @event.FailedMessageId
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts)
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