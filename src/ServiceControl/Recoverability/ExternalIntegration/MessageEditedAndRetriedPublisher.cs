namespace ServiceControl.Recoverability.ExternalIntegration;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.MessageFailures;
using ExternalIntegrations;

class MessageEditedAndRetriedPublisher : EventPublisher<MessageEditedAndRetried,
    MessageEditedAndRetriedPublisher.DispatchContext>
{
    protected override DispatchContext CreateDispatchRequest(MessageEditedAndRetried @event) =>
        new()
        {
            FailedMessageId = @event.FailedMessageId,
        };

    protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts) =>
        Task.FromResult(contexts.Select(x => (object)new Contracts.MessageEditedAndRetried
        {
            FailedMessageId = x.FailedMessageId,
        }));

    public class DispatchContext
    {
        public string FailedMessageId { get; set; }
    }

}