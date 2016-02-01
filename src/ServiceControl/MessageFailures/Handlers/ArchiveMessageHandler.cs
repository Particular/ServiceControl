namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    public class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        public IDocumentSession Session { get; set; }

        public async Task Handle(ArchiveMessage message, IMessageHandlerContext context)
        {
            var failedMessage = Session.Load<FailedMessage>(new Guid(message.FailedMessageId));

            if (failedMessage == null)
            {
                return; //No point throwing
            }

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                failedMessage.Status = FailedMessageStatus.Archived;

                await context.Publish<FailedMessageArchived>(m=>m.FailedMessageId = message.FailedMessageId);
            }
        }
    }
}