namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    class UnarchiveAllInGroupHandler : IHandleMessages<UnarchiveAllInGroup>
    {
        public UnarchiveAllInGroupHandler(IArchiveMessages archiver, RetryingManager retryingManager, ILogger<UnarchiveAllInGroupHandler> logger)
        {
            this.archiver = archiver;
            this.retryingManager = retryingManager;
            this.logger = logger;
        }

        public async Task Handle(UnarchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.LogWarning("Attempt to unarchive a group ({messageGroupId}) which is currently in the process of being retried", message.GroupId);
                return;
            }

            await archiver.UnarchiveAllInGroup(message.GroupId);
        }

        IArchiveMessages archiver;
        RetryingManager retryingManager;
        readonly ILogger<UnarchiveAllInGroupHandler> logger;
    }
}