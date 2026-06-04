namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [Handler]
    class ArchiveAllInGroupHandler(IArchiveMessages archiver, RetryingManager retryingManager, ILogger<ArchiveAllInGroupHandler> logger) : IHandleMessages<ArchiveAllInGroup>
    {
        public async Task Handle(ArchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.LogWarning("Attempt to archive a group ({MessageGroupId}) which is currently in the process of being retried", message.GroupId);
                return;
            }

            await archiver.ArchiveAllInGroup(message.GroupId);
        }
    }
}
