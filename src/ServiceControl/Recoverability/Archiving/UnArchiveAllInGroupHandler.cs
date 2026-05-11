namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Persistence.Recoverability;

    [Handler]
    class UnarchiveAllInGroupHandler(IArchiveMessages archiver, RetryingManager retryingManager, ILogger<UnarchiveAllInGroupHandler> logger) : IHandleMessages<UnarchiveAllInGroup>
    {
        public async Task Handle(UnarchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.LogWarning("Attempt to unarchive a group ({MessageGroupId}) which is currently in the process of being retried", message.GroupId);
                return;
            }

            await archiver.UnarchiveAllInGroup(message.GroupId);
        }
    }
}