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
            EditId = @event.EditId,
            FailedMessageId = @event.FailedMessageId,
            RetriedMessageId = @event.FailedMessageId
        };

    protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts) =>
        Task.FromResult(contexts.Select(x => (object)new Contracts.MessageEditedAndRetried
        {
            EditId = x.EditId,
            FailedMessageId = x.FailedMessageId,
            RetriedMessageId = x.RetriedMessageId
        }));

    public class DispatchContext
    {
        public string FailedMessageId { get; set; }
        public string RetriedMessageId { get; set; }
        public string EditId { get; set; }
    }

}