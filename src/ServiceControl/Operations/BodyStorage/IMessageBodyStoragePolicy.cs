namespace ServiceControl.Operations.BodyStorage
{
    public interface IMessageBodyStoragePolicy
    {
        bool ShouldStore(MessageBodyMetadata messageBodyMetadata);
        bool ShouldIndex(MessageBodyMetadata messageBodyMetadata);
    }
}