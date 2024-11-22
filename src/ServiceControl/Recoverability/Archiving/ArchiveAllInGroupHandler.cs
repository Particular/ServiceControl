namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Persistence.Recoverability;

    class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public ArchiveAllInGroupHandler(IArchiveMessages archiver, RetryingManager retryingManager)
        {
            this.archiver = archiver;
            this.retryingManager = retryingManager;
        }

        public async Task Handle(ArchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                Log.Warn($"Attempt to archive a group ({message.GroupId}) which is currently in the process of being retried");
                return;
            }

            await archiver.ArchiveAllInGroup(message.GroupId);
        }

        readonly IArchiveMessages archiver;
        readonly RetryingManager retryingManager;

        static ILog Log = LogManager.GetLogger<ArchiveAllInGroupHandler>();
    }
}