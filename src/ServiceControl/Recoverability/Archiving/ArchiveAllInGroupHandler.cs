namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Persistence.Recoverability;

    class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public ArchiveAllInGroupHandler(IArchiveMessages archiver, IDomainEvents domainEvents, RetryingManager retryingManager)
        {
            this.archiver = archiver;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(ArchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.Warn($"Attempt to archive a group ({message.GroupId}) which is currently in the process of being retried");
                return;
            }

            await archiver.ArchiveAllInGroup(message.GroupId, domainEvents).ConfigureAwait(false);
        }

        IArchiveMessages archiver;
        IDomainEvents domainEvents;
        RetryingManager retryingManager;

        static ILog logger = LogManager.GetLogger<ArchiveAllInGroupHandler>();
    }
}
