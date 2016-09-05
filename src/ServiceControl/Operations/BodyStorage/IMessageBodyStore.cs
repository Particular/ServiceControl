namespace ServiceControl.Operations.BodyStorage
{
    public interface IMessageBodyStore
    {
        ClaimsCheck Store(byte[] messageBody, MessageBodyMetadata messageBodyMetadata, IMessageBodyStoragePolicy messageStoragePolicy);
        bool TryGet(string messageId, out byte[] messageBody, out MessageBodyMetadata messageBodyMetadata);
    }
}