namespace ServiceControl.Recoverability.ExternalIntegration;

using System.Collections.Generic;
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