namespace ServiceControl.Operations.Error
{
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations.BodyStorage;

    class ErrorMessageBodyStoragePolicy : IMessageBodyStoragePolicy
    {
        const int LargeObjectHeapThreshold = 85 * 1024;
        private Settings settings;

        public ErrorMessageBodyStoragePolicy(Settings settings)
        {
            this.settings = settings;
        }

        public bool ShouldStore(MessageBodyMetadata messageBodyMetadata) 
            => messageBodyMetadata.Size > 0;

        public bool ShouldIndex(MessageBodyMetadata messageBodyMetadata)
            => (messageBodyMetadata.Size <= settings.MaxBodySizeToStore) && AvoidsLargeObjectHeap(messageBodyMetadata) && !IsBinary(messageBodyMetadata);

        private bool IsBinary(MessageBodyMetadata messageBodyMetadata)
            => messageBodyMetadata.ContentType.Contains("binary");

        private bool AvoidsLargeObjectHeap(MessageBodyMetadata messageBodyMetadata)
            => messageBodyMetadata.Size < LargeObjectHeapThreshold;
    }
}