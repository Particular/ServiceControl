namespace ServiceControl.Operations.Audit
{
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations.BodyStorage;

    class AuditMessageBodyStoragePolicy : IMessageBodyStoragePolicy
    {
        const int LargeObjectHeapThreshold = 85 * 1024;
        private Settings settings;

        public AuditMessageBodyStoragePolicy(Settings settings)
        {
            this.settings = settings;
        }

        public bool ShouldStore(MessageBodyMetadata messageBodyMetadata)
            => messageBodyMetadata.Size > 0 && messageBodyMetadata.Size <= settings.MaxBodySizeToStore;

        public bool ShouldIndex(MessageBodyMetadata messageBodyMetadata)
            => !IsBinary(messageBodyMetadata) && AvoidsLargeObjectHeap(messageBodyMetadata);

        private bool IsBinary(MessageBodyMetadata messageBodyMetadata)
            => messageBodyMetadata.ContentType.Contains("binary");

        private bool AvoidsLargeObjectHeap(MessageBodyMetadata messageBodyMetadata)
            => messageBodyMetadata.Size < LargeObjectHeapThreshold;
    }
}