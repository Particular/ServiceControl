namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.RavenDB;

    public class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        public RavenUnitOfWork RavenUnitOfWork { get; set; }

        public void Handle(ArchiveMessage message)
        {
            var failedMessage = RavenUnitOfWork.Session.Load<FailedMessage>(message.FailedMessageId);

            if (failedMessage == null)
            {
                return; //No point throwing
            }

            failedMessage.Status = FailedMessageStatus.Archived;
        }
    }
}