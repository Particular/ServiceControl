namespace ServiceControl.MessageFailures.Handlers
{
    using Contracts.MessageFailures;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.RavenDB;

    public class FailedMessageArchivedHandler : IHandleMessages<FailedMessageArchived>
    {
        public RavenUnitOfWork RavenUnitOfWork { get; set; }

        public void Handle(FailedMessageArchived message)
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