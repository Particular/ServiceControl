namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public ArchiveAllInGroupHandler(IArchiveMessages archiver, RetryingManager retryingManager, ILogger<ArchiveAllInGroupHandler> logger)
        {
            this.archiver = archiver;
            this.retryingManager = retryingManager;
            this.logger = logger;
        }

        public async Task Handle(ArchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.LogWarning("Attempt to archive a group ({MessageGroupId}) which is currently in the process of being retried", message.GroupId);
                return;
            }

            await archiver.ArchiveAllInGroup(message.GroupId);
        }

        readonly IArchiveMessages archiver;
        readonly RetryingManager retryingManager;
        readonly ILogger<ArchiveAllInGroupHandler> logger;
    }
}
