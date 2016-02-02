namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolvedByRetry>
    {
        public IDocumentSession Session { get; set; }

        public Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            var failedMessage = Session.Load<FailedMessage>(new Guid(message.FailedMessageId));

            if (failedMessage != null)
            {
                failedMessage.Status = FailedMessageStatus.Resolved;
            }

            return Task.FromResult(0); //No point throwing
        }
    }
}