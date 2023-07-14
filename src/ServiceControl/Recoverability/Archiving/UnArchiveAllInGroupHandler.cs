namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Persistence.Recoverability;

    class UnarchiveAllInGroupHandler : IHandleMessages<UnarchiveAllInGroup>
    {
        public UnarchiveAllInGroupHandler(IArchiveMessages archiver, IDomainEvents domainEvents, RetryingManager retryingManager)
        {
            this.archiver = archiver;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(UnarchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.Warn($"Attempt to unarchive a group ({message.GroupId}) which is currently in the process of being retried");
                return;
            }

            await archiver.UnarchiveAllInGroup(message.GroupId, domainEvents).ConfigureAwait(false);
        }

        IArchiveMessages archiver;
        IDomainEvents domainEvents;
        RetryingManager retryingManager;

        static ILog logger = LogManager.GetLogger<UnarchiveAllInGroupHandler>();
    }
}
