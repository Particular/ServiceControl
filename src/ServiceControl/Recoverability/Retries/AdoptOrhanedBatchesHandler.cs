namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class AdoptOrhanedBatchesHandler : IHandleMessages<AdoptOrphanedBatches>
    {
        public void Handle(AdoptOrphanedBatches message)
        {
            if (RetryDocumentManager != null)
            {
                RetryDocumentManager.AdoptOrphanedBatches(message.StartupTime);
            }
        }

        public RetryDocumentManager RetryDocumentManager { get; set; }
    }
}